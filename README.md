# AutoJot

A bot that can handle note search and automatically organized upserts in markdown format.

## Overview
AutoJot is a C#/.NET bot designed to help users manage notes efficiently. It supports searching notes and automatically updating or inserting them in markdown files. The bot leverages AI services for note classification, keyword extraction, and content organization.

## Features
- **Note Search:** Quickly find notes using keywords or phrases.
- **Automatic Upserts:** Add or update notes in markdown format, keeping your notes organized.
- **AI Integration:** Uses AI models (Ollama, OpenAI) for note creation, update, and keyword extraction.
- **Markdown Organization:** Notes are stored and managed in markdown files for easy readability and portability.
- **Indexed Search:** Notes are indexed once and kept up to date as they change, so searching does not re-read your whole vault on every message.
- **Telegram Bot Support:** Interact with the bot via Telegram (see `TelegramBotWorker`).

## Architecture
- **AppHost:** Aspire host that launches the worker and serves the telemetry dashboard.
- **ServiceDefaults:** OpenTelemetry wiring shared by every host that runs AutoJot.
- **AutoJot.Core:** Core library — bot state machine, AI services, file handling, and shared utilities.
- **AutoJot.Worker:** Background worker for Telegram bot integration. This is the entry point.
- **UnitTest:** xUnit test project.

Package versions are managed centrally in `Directory.Packages.props`, and `global.json` pins the
.NET SDK to 9.x.

### Search
Notes are scored on three signals, weighted in that order: the file name (3), front matter tags (2),
and the note text (1). Repetition in the body is capped, so a note that happens to say "cake" forty
times cannot bury the note actually named for it.

The vault is indexed in memory at startup and watched for changes, so notes you edit in Obsidian
show up without restarting the bot. Where a folder cannot be watched — some network and synced
folders refuse — it falls back to comparing timestamps.

### Telemetry
Running through the AppHost (`dotnet run --project AppHost`) starts the Aspire dashboard, which
shows traces for each message the bot handles, including how long every AI call took and which
model served it. Running `AutoJot.Worker` on its own is still fine — with no collector configured
the exporter is simply left off.

Telegram puts the bot token in the request path, so the URL is redacted before it reaches a span or
a log line. If you add instrumentation of your own, keep it that way.

## Setup & Installation
**Prerequisites:** .NET SDK 9.x (see `global.json`).

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
   - See [Configuration](#configuration) below. Secrets go in user secrets or environment variables, never in `appsettings.json`.

## Configuration

Secrets never belong in `appsettings.json` — that file is committed. Use .NET user secrets in
development (the Worker already declares a `UserSecretsId`) or environment variables in production.

```sh
dotnet user-secrets --project AutoJot.Worker set "AutoJot:RootPath" "/path/to/your/notes"
dotnet user-secrets --project AutoJot.Worker set "Telegram:BotToken" "<your bot token>"
dotnet user-secrets --project AutoJot.Worker set "OpenAi:ApiKey" "<your api key>"
```

The equivalent environment variables use `__` as the separator, e.g. `Telegram__BotToken`.

| Setting | Purpose |
| --- | --- |
| `AutoJot:RootPath` | Absolute path to your notes vault. |
| `AutoJot:AiProvider` | `OpenAi` or `Ollama`. Defaults to Ollama. |
| `AutoJot:AllowedUserIds` | Telegram user ids allowed to use the bot. **Required** — see below. |
| `OpenAi:ApiKey` / `OpenAi:Model` | OpenAI credentials and model. |
| `Ollama:Endpoint` / `Ollama:Model` | Local Ollama server and model. |
| `Telegram:BotToken` | Bot token from BotFather. |

### Access control

The bot reads and overwrites everything in your vault, so it only answers users listed in
`AutoJot:AllowedUserIds`. The list starts empty, which rejects everyone. Message the bot once and
your user id will appear in the logs:

```
warn: Rejected message from unauthorized Telegram user id 123456789.
```

Add it to your configuration and restart:

```sh
dotnet user-secrets --project AutoJot.Worker set "AutoJot:AllowedUserIds:0" "123456789"
```

## Usage
- **Run the bot:**
   ```sh
   dotnet run --project AppHost
   ```

1. When all setup and running, you may type out anything in the bot chat.
2. It'll try to classify your message as a **query** or an **upsert** and get some keywords based on your input: 
   1. **Query**:
      1. A list of best file matches (based on the file name, tags and text of your notes, scored against the keywords from your input) will be returned.
      2. The bot will ask which file you want to see the contents of, and after your selection it will return it.
   2. **Upsert**:
      1. A list of best file matches (based on the file name, tags and text of your notes, scored against the keywords from your input) will be returned. You may select which file you want to update based on your input, or you may choose to create a new file. 
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

Wherever the bot offers you a choice, it sends tappable buttons rather than a numbered list — pick
one and it carries on. A menu from an earlier, finished conversation is recognised and rejected
rather than acted on, so an accidental tap on old buttons is harmless.

### Commands
| Command | Effect |
| --- | --- |
| `/search <keywords>` | Search, skipping the classification step. |
| `/upsert <text>` | Save, skipping the classification step. |
| `/cancel` | Forget the current conversation and start over. |
| `/help`, `/start` | Show what the bot can do. |

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

