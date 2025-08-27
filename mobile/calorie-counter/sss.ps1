# ���� capacitor.config.ts
(Get-Content -Raw .\capacitor.config.ts) `
  -replace "webDir:\s*'[^']+'", "webDir: 'dist/calorie-counter/browser'" `
  -replace "webDir:\s*\"[^\"]+\"", "webDir: 'dist/calorie-counter/browser'" `
| Set-Content .\capacitor.config.ts -Encoding UTF8

# ���� ����� ���� capacitor.config.json (� �� ������������) � �������� � ���
if (Test-Path .\capacitor.config.json) {
  $j = Get-Content .\capacitor.config.json -Raw | ConvertFrom-Json
  $j.webDir = "dist/calorie-counter/browser"
  $j | ConvertTo-Json -Depth 5 | Set-Content .\capacitor.config.json -Encoding UTF8
}
