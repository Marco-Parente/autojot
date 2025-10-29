namespace Core.Services.AI;

public interface IAiService
{
    Task<MessageClassificationResult> ClassifyMessage(string userInput);
    Task<CreateNoteResult> CreateNote(string userInput, List<string> existingFolders);
    Task<UpdateNoteResult> UpdateNote(string input, string existingFileContent);
    Task<List<string>> GetKeyWords(string input);

    public static class Types
    {
        public const string Ollama = "Ollama";
        public const string OpenAi = "OpenAi";
    }
}

public class CreateNoteResult : IPrompt
{
    public string FilePath { get; set; } = null!;
    public string Content { get; set; } = null!;

    #region PromptProperties
    public static string JsonSchema =>
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
            """;

    public static string JsonSchemaDescription =>
        "The new note content and its suggested file path";

    public static string OpenAiInstructions =>
        """
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

    public static string OllamaInstructions => OpenAiInstructions;

    #endregion
}

public class UpdateNoteResult : IPrompt
{
    public string Content { get; set; } = null!;

    #region PromptProperties
    public static string JsonSchema =>
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
            """;

    public static string JsonSchemaDescription => "The updated note content";

    public static string OpenAiInstructions =>
        """
            You are an assistant that updates an existing Markdown note by integrating new information.
                
            Rules:

            1. Input:
               - You will receive:
                 * The current Markdown note (including YAML Front Matter)
                 * New input text containing updated or corrective information.

            2. Integration behavior:
               - Your job is to **directly modify the relevant parts of the note** so they reflect the new input.
               - **Do not create new sections or notes** unless the new input introduces a *completely new concept* that cannot logically be integrated into existing sections.
               - If the new input *corrects* or *updates* a value, instruction, or detail, **replace the old information** in the relevant sentence or step.
               - Do **not** add commentary, justifications, or “important correction” notes — update the text itself.

            3. Formatting:
               - Maintain all Markdown formatting, structure, and hierarchy (headings, lists, code blocks, etc.).
               - Preserve the writing tone and style of the original note.

            4. YAML Front Matter:
               - Keep `created_at` unchanged.
               - Add or update `updated_at` with the current date in ISO 8601 format.
               - Update `tags` (max 5) to reflect the note's content if necessary. Tags must be single or hyphenated words.

            5. Output:
               - Return the **entire updated Markdown note**, including YAML Front Matter.
               - Do not include explanations or meta commentary — output only the final, updated note.
            """;

    public static string OllamaInstructions => OpenAiInstructions;

    #endregion
}

public class MessageClassificationResult : IPrompt
{
    public ClassificationType ClassificationType { get; set; }
    public List<string> Keywords { get; set; } = [];

    #region PromptProperties
    public static string JsonSchema =>
        // language=json
        """
            {
                "type": "object",
                "properties": {
                  "classificationType": {"enum": ["Query", "Upsert"]},
                  "keywords": {"type": "array", "items": {"type": "string"}},
                  "reasoning": {"type": "string"}
                },
                "required": ["classificationType", "keywords", "reasoning"],
                "additionalProperties": false
            }
            """;

    public static string JsonSchemaDescription =>
        "The classification of the input and its keywords";

    public static string OpenAiInstructions =>
        """
            System Role: Binary Classifier for QUERY/UPSERT

            **Objective:** Determine whether the input text is a **QUERY** or an **UPSERT** based on the
            following definitions:

            **Definitions:**
            - **QUERY**: The user is asking, searching, or providing short topic keywords. Typical indicators
            include:
              - The sentence is a question or contains a question mark.
              - It is a noun phrase or short fragment.
              - Crucially, it does not assert anything new (does not state a fact or update knowledge).

            - **UPSERT**: The user is asserting, informing, or updating knowledge. Typical indicators
            include:
              - Contains verbs.
              - Sounds like a statement or fact (provides new information or confirms existing knowledge).
              - Lacks question marks or request tone.

            - If the text is ambiguous, analyze the **strongest indicator** and choose the classification
            accordingly.

            - Generate up to 10 keywords related to the main concepts in the input.
                - Each keyword must be one word or a hyphenated word (no spaces).
            """;

    public static string OllamaInstructions => OpenAiInstructions;

    #endregion
}

public class GetKeywordsResult : IPrompt
{
    public List<string> Keywords { get; set; } = [];

    #region  PromptProperties
    public static string JsonSchema =>
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
            """;

    public static string JsonSchemaDescription => "Keywords of the input";

    public static string OpenAiInstructions =>
        """
            You are an assistant that gets keywords from the input. 
            Instructions:

                * Get the 5 most relevant keywords from the input
                * The keywords must be one word or a hyphenated word (no spaces).
            """;

    public static string OllamaInstructions => OpenAiInstructions;

    #endregion
}

public interface IPrompt
{
    static abstract string JsonSchema { get; }
    static abstract string JsonSchemaDescription { get; }
    static abstract string OpenAiInstructions { get; }
    static abstract string OllamaInstructions { get; }
}

public enum ClassificationType
{
    Query = 10,
    Upsert = 20,
}
