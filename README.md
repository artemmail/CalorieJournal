# CalorieJournal

## Configuration

The application uses [Pandoc](https://pandoc.org/) to render PDF reports.
The following configuration keys can be provided (e.g., via environment
variables or `appsettings.json`):

| Key | Description | Default |
| --- | --- | --- |
| `PandocPath` | Path to the `pandoc` executable | `pandoc` |
| `PandocWorkingDirectory` | Working directory for Pandoc | current directory |
| `PandocMainFont` | Font used when generating PDFs | `DejaVu Sans` |

`PandocMainFont` defaults to **DejaVu Sans**, a Unicode font that supports
Cyrillic characters.
