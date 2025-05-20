# Power Commander

Power Commander is a Windows system tray application that automatically prompts for shutdown at scheduled times. If no response is given, the machine shuts down, simples!

## Features

- Scheduled shutdowns via `settings.json`
- A tray icon...wooo

## Configuration

Create a `settings.json` file in the same folder as the executable:

```json
{
  "loadService": true,
  "shutdownTimes": ["21:00", "23:45"],
  "showNextScheduledTime": true,
  "enableExitOption": true,
  "enableManualShutdown": true
}
