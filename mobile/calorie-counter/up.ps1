$ErrorActionPreference = "Stop"

function Patch-File($path, $replacements) {
  if (-not (Test-Path $path)) { Write-Error "Not found: $path"; return }
  $c = Get-Content -Raw $path
  foreach ($r in $replacements) { $c = [regex]::Replace($c, $r.Pattern, $r.Repl) }
  Set-Content -LiteralPath $path -Value $c -Encoding UTF8
  Write-Host "Patched: $path"
}

# 1) HistoryPage: импорт и DI типа
Patch-File "src/app/pages/history/history.page.ts" @(
  @{ Pattern = 'import\s*\{\s*AuthService\s*\}\s*from\s*"..\/..\/services\/auth\.service";'; Repl = 'import { FoodBotAuthLinkService } from "../../services/foodbot-auth-link.service";' }
  @{ Pattern = 'private\s+auth\s*:\s*AuthService'; Repl = 'private auth: FoodBotAuthLinkService' }
)

# 2) FoodbotApiService: правильный импорт и тип
Patch-File "src/app/services/foodbot-api.service.ts" @(
  @{ Pattern = 'import\s*\{\s*AuthService\s*\}\s*from\s*"\.\/foodbot-auth-link\.service";'; Repl = 'import { FoodBotAuthLinkService } from "./foodbot-auth-link.service";' }
  @{ Pattern = 'import\s*\{\s*FoodBotAuthLinkService\s*\}\s*from\s*"\.\/auth\.service";'; Repl = 'import { FoodBotAuthLinkService } from "./foodbot-auth-link.service";' }
  @{ Pattern = 'private\s+auth\s*:\s*AuthService'; Repl = 'private auth: FoodBotAuthLinkService' }
  @{ Pattern = 'constructor\(\s*private\s+http:\s*HttpClient,\s*private\s+auth:\s*[A-Za-z0-9_]+\s*\)'; Repl = 'constructor(private http: HttpClient, private auth: FoodBotAuthLinkService)' }
  @{ Pattern = 'private\s+get\s+baseUrl\(\)\s*:\s*string\s*\{[\s\S]*?\}'; Repl = 'private get baseUrl(): string { return this.auth.apiBaseUrl; }' }
)

Write-Host "`nDone. If you still see errors, search leftovers:"
Write-Host '  Select-String -Path src\app\**\*.ts -Pattern "AuthService" -List | % { $_.Path }'
