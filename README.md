# AutoJot

A bot that can handle note search and automatically organized upserts in markdown format.

## Overview
AutoJot is a C#/.NET bot designed to help users manage notes efficiently. It supports searching notes and automatically updating or inserting them in markdown files. The bot leverages AI services for note classification, keyword extraction, and content organization.

## Features
- **Note Search:** Quickly find notes using keywords or phrases.
- **Automatic Upserts:** Add or update notes in markdown format, keeping your notes organized.
- **AI Integration:** Uses AI models (Ollama, OpenAI) for note creation, update, and keyword extraction.
- **Markdown Organization:** Notes are stored and managed in markdown files for easy readability and portability.
- **Telegram Bot Support:** Interact with the bot via Telegram (see `TelegramBotWorker`).

## Architecture
- **AppHost:** Main application host and configuration.
- **AutoJot.Core:** Core logic, including bot service, AI services, file handling, and shared utilities.
- **AutoJot.Worker:** Background worker for Telegram bot integration.
- **UnitTest:** Simple unit test project.

## Setup & Installation
1. **Clone the repository:**
   ```sh
   git clone <repo-url>
   cd AutoJot
   ```
2. **Restore dependencies:**
   ```sh
   dotnet restore
   ```
3. **Build the solution:**
   ```sh
   dotnet build
   ```
4. **Configure environment:**
   - Edit `appsettings.json` and `appsettings.Development.json` in each project as needed.

## Configuration
- **AI Services:** Configure API keys and endpoints for OpenAI or your local Ollama server in the settings files.
- **Telegram Bot:** Set your bot token and other options in `AutoJot.Worker/appsettings.json`.

## Usage
- **Run the bot:**
   ```sh
   dotnet run --project AppHost
   ```

1. When all setup and running, you may type out anything in the bot chat.
2. It'll try to classify your message as a **query** or an **upsert** and get some keywords based on your input: 
   1. **Query**:
      1. A list of best file matches (based on the file name, tags and the keywords gotten from your input) will be returned.
      2. The bot will ask which file you want to see the contents of, and after your selection it will return it.
   2. **Upsert**:
      1. A list of best file matches (based on the file name, tags and the keywords gotten from your input) will be returned. You may select which file you want to update based on your input, or you may choose to create a new file. 
         1. **New note** - if this option is selected, the bot will:
            1. Reformat the content in a Markdown formatting
            2. Add relevant tags based on file content
            3. Automatically decide the file path and filename in which the content will be saved
            4. Return the content of the newly created file
         2. **Existing note** - if an existing note is selected, the bot will:
            1. Update the note content based on your new input
            2. Update the tags, if necessary
            3. Update the 'updated_at' tag
            4. Return the updated file content and ask if the user wants to save os discard the changes made

- You may also provide your intent to skip the classification part of the flow  by prepending your input with '/search' or '/upsert'

## Example usages
### Querying for an existing note
<img width="742" height="427" alt="image" src="https://github.com/user-attachments/assets/dfdd0fbd-343f-445d-b732-9598d665147b" />

---

### Creating a new note
- Recipe copied and paste from internet:
<img width="736" height="552" alt="image" src="https://github.com/user-attachments/assets/fc89f862-3124-444e-95ab-25141f686b21" />

- Bot detected as an upsert, choose if i want to update a file or create a new one (sorry for the censored part :p):
<img width="715" height="516" alt="image" src="https://github.com/user-attachments/assets/95237bde-2b27-40e2-b9ae-46b582a6c932" />

- New note is created
<img width="705" height="377" alt="image" src="https://github.com/user-attachments/assets/5870cec5-a287-4c26-aa86-7cd82b7ae574" />

- Great to use with apps like Obsidian, as it gets rendered nicely:
<img width="626" height="625" alt="image" src="https://github.com/user-attachments/assets/ab610644-0399-47cb-bc38-b5c99bcc77a5" />

---

### Updating an existing note
- Just said to the bot the chocolate cake should be in the oven for 3 hours (dont worry, it's just an example 🧯🔥)
<img width="468" height="300" alt="image" src="https://github.com/user-attachments/assets/c1cd6f37-a7bd-4174-b8c6-4c79ed1b149d" />


- The relevant part of the note gets automatically updated:
<img width="742" height="557" alt="image" src="https://github.com/user-attachments/assets/0cd96a9d-61f3-490f-b4d5-6f02c54ba544" />


## Notes
- You may check how to create your own Telegram bot in [here](https://core.telegram.org/bots/tutorial)
- I've tried to add an Ollama service to use DeepSeek, but wasn't able to get good results. I'd got better ones using "gpt-5-nano" from OpenAI. But feel free to try using whichever you prefer and changing the prompts to better suit your needs.

## Contributing
Contributions are welcome! Please open issues or submit pull requests for improvements or bug fixes :)

## License
This project is licensed under the MIT License.

