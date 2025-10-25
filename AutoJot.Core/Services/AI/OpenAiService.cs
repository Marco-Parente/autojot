using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using OpenAI.Responses;

#pragma warning disable OPENAI001

namespace Core.Services.AI;

public class OpenAiService : IAiService
{
    private const string Model = "gpt-5-nano";

    private readonly IConfiguration _configuration;

    public OpenAiService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public async Task<MessageClassificationResult> ClassifyMessage(string userInput)
    {
        var conversationHistory = new List<dynamic> { new { role = "user", content = userInput } };

        var client = new OpenAIResponseClient(
            Model,
            _configuration.GetValue<string?>("OpenAi:ApiKey")
                ?? throw new Exception("The open ai api key is missing.")
        );

        var options = new ResponseCreationOptions
        {
            TextOptions = new ResponseTextOptions
            {
                TextFormat = ResponseTextFormat.CreateJsonSchemaFormat(
                    "autojot_output",
                    BinaryData.FromBytes(
                        Encoding.UTF8.GetBytes(
                            // language=json
                            """
                            {
                                "type": "object",
                                "properties": {
                                  "classificationType": {"enum": ["Query", "Upsert"]},
                                  "keywords": {"type": "array", "items": {"type": "string"}}
                                },
                                "required": ["classificationType", "keywords"],
                                "additionalProperties": false
                            }
                            """
                        )
                    ),
                    "The classification of the input and its keywords",
                    true
                ),
            },
            Instructions = """
                You are an assistant that classifies the user’s input to determine its purpose.

                1. A query — the user is looking for information or trying to retrieve an existing note.
                2. information — the user is providing new content to add or update in a note.

                ---

                Instructions:**                
                1. If the input asks a question, requests something (“show,” “find,” “how,” “what,” “can you,” etc.), or seems to seek information, classify it as a query.
                2. If the input states facts, gives instructions, describes something, adds details, or sounds like note content, classify it as information.
                3. Short inputs:
                   * If short but declarative (e.g., “hamburguer recipe should have 300g of meat”), treat as information.
                   * If short and ambiguous, treat as a query.
                   
                4. Generate up to 10 keywords related to the main concepts in the input.
                   * Each keyword must be one word or a hyphenated word (no spaces).
                   * Choose words that best represent the input’s topics or entities.
                """,
        };

        var response = await client.CreateResponseAsync(
            JsonSerializer.Serialize(conversationHistory, _jsonOptions),
            options
        );

        var final = JsonSerializer.Deserialize<MessageClassificationResult>(
            response.Value.GetOutputText(),
            _jsonOptions
        );

        return final!;
    }

    public async Task<CreateNoteResult> CreateNote(
        string input,
        List<string>? existingFolders = null
    )
    {
        var conversationHistory = new List<dynamic>
        {
            new { type = "new-input", content = input },
            new { type = "existing-folders", content = existingFolders },
        };

        const string instructions = """
            You are an assistant that generates a well-formatted Markdown note from user-provided text.

            {
                "content": <the generarted markdown content>,
                "filePath": <suggested filePath for this content>
            }

            * FilePath Instructions:

            1. Suggest a file path for saving the note based on its topic and content:
                * Use an existing directory if it fits, or propose a new one.
                * It must include the name of the file
                * Example: `"recipes/desserts/chocolate-cake.md"`

            *Content Instructions:

            1. Reorganize and format the text to improve readability without changing its meaning or removing important details.
            2. Add a YAML Front Matter block at the top:

               ```yaml
               ---
               tags: [tag1, tag2, tag3, tag4, tag5]
               created_at: YYYY-MM-DD
               ---
               ```

               * Generate at most 5 tags based on the most relevant keywords.
               * Each tag must be a single word or a hyphenated word (no spaces).
               
            3. Format the rest of the note using standard Markdown (headings, lists, code blocks, bold/italic).
            """;

        var client = new OpenAIResponseClient(
            Model,
            _configuration.GetValue<string?>("OpenAi:ApiKey")
                ?? throw new Exception("The open ai api key is missing.")
        );

        var options = new ResponseCreationOptions
        {
            TextOptions = new ResponseTextOptions
            {
                TextFormat = ResponseTextFormat.CreateJsonSchemaFormat(
                    "autojot_output",
                    BinaryData.FromBytes(
                        Encoding.UTF8.GetBytes(
                            // language=json
                            """
                            {
                                "type": "object",
                                "properties": {
                                  "filePath": {"type": "string" },
                                  "content": {"type": "string" }
                                },
                                "required": ["filePath", "content"],
                                "additionalProperties": false
                            }
                            """
                        )
                    ),
                    "The classification of the input and its keywords",
                    true
                ),
            },
            Instructions = instructions,
        };

        var response = await client.CreateResponseAsync(
            JsonSerializer.Serialize(conversationHistory, _jsonOptions),
            options
        );

        var final = JsonSerializer.Deserialize<CreateNoteResult>(
            response.Value.GetOutputText(),
            _jsonOptions
        );

        return final!;
    }

    public async Task<UpdateNoteResult> UpdateNote(string input, string existingFileContent)
    {
        var conversationHistory = new List<dynamic>
        {
            new { type = "new-input", content = input },
            new { type = "existing-file-content", content = existingFileContent },
        };

        const string instructions = """
            You are an assistant that updates an existing Markdown note by integrating the new information provided

            Instructions:            
            1. Incorporate new content logically, updating existing instructions, steps, or notes if the new information corrects or modifies them.
               * Do not just insert the literal input text. Search the context and update accordingly.

            2. Keep all formatting and structure consistent with Markdown (headings, lists, paragraphs, code blocks).
            3. Update YAML Front Matter:
                * Keep `created_at` unchanged.
                * Add/update `updated_at` with current date in ISO 8601 format.
                * You can update `tags` to a maximum of 5, if necessary.
                    * Tags should be only single or hyphenated words representing the note’s content.
                
            4. Preserve all other content unless it must be adjusted for clarity or consistency.
            5. Output the complete updated Markdown note, including the full YAML Front Matter block.
            """;

        var client = new OpenAIResponseClient(
            Model,
            _configuration.GetValue<string?>("OpenAi:ApiKey")
                ?? throw new Exception("The open ai api key is missing.")
        );

        var options = new ResponseCreationOptions
        {
            TextOptions = new ResponseTextOptions
            {
                TextFormat = ResponseTextFormat.CreateJsonSchemaFormat(
                    "autojot_output",
                    BinaryData.FromBytes(
                        Encoding.UTF8.GetBytes(
                            // language=json
                            """
                            {
                                "type": "object",
                                "properties": {
                                  "content": {"type": "string"}
                                },
                                "required": ["content"],
                                "additionalProperties": false
                            }
                            """
                        )
                    ),
                    "The classification of the input and its keywords",
                    true
                ),
            },
            Instructions = instructions,
        };

        var response = await client.CreateResponseAsync(
            JsonSerializer.Serialize(conversationHistory, _jsonOptions),
            options
        );

        var final = JsonSerializer.Deserialize<UpdateNoteResult>(
            response.Value.GetOutputText(),
            _jsonOptions
        );

        return final!;
    }

    public async Task<List<string>> GetKeyWords(string input)
    {
        var conversationHistory = new List<dynamic> { new { type = "input", content = input } };

        const string instructions = """
            You are an assistant that gets keywords from the input. 
            Instructions:

                * Get the 5 most relevant keywords from the input
                * The keywords must be one word or a hyphenated word (no spaces).
            """;

        var client = new OpenAIResponseClient(
            Model,
            _configuration.GetValue<string?>("OpenAi:ApiKey")
                ?? throw new Exception("The open ai api key is missing.")
        );

        var options = new ResponseCreationOptions
        {
            TextOptions = new ResponseTextOptions
            {
                TextFormat = ResponseTextFormat.CreateJsonSchemaFormat(
                    "autojot_output",
                    BinaryData.FromBytes(
                        Encoding.UTF8.GetBytes(
                            // language=json
                            """
                            {
                                "type": "object",
                                "properties": {
                                  "keywords": {"type": "array", "items": {"type": "string"}}
                                },
                                "required": ["keywords"],
                                "additionalProperties": false
                            }
                            """
                        )
                    ),
                    "Keywords of the input",
                    true
                ),
            },
            Instructions = instructions,
        };

        var response = await client.CreateResponseAsync(
            JsonSerializer.Serialize(conversationHistory, _jsonOptions),
            options
        );

        var final = JsonSerializer.Deserialize<GetKeywordsResult>(
            response.Value.GetOutputText(),
            _jsonOptions
        );

        return final!.Keywords;
    }

    public async Task Test()
    {
        var client = new OpenAIResponseClient(
            Model,
            _configuration.GetValue<string?>("OpenAi:ApiKey")
                ?? throw new Exception("The open ai api key is missing.")
        );

        var options = new ResponseCreationOptions
        {
            TextOptions = new ResponseTextOptions
            {
                TextFormat = ResponseTextFormat.CreateJsonSchemaFormat(
                    "autojot_output",
                    BinaryData.FromBytes(
                        Encoding.UTF8.GetBytes(
                            // language=json
                            """
                            {
                                "type": "object",
                                "properties": {
                                  "texto": {"type": "string"}
                                },
                                "required": ["texto"],
                                "additionalProperties": false
                            }
                            """
                        )
                    ),
                    "Texto",
                    true
                ),
            },
            Instructions = "Retorne o mesmo texto que for mandado",
        };

        var response = await client.CreateResponseAsync(
            "Retorne esse texto: 'O café estava muito quente: 98°'",
            options
        );
    }
}
