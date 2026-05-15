
import re

file_path = 'FluidSetup.vdproj'
with open(file_path, 'r', encoding='utf-8') as f:
    content = f.read()

# 1. ブートストラップの無効化（8007006E エラー対策）
content = content.replace('"Enabled" = "11:TRUE"', '"Enabled" = "11:FALSE"')

# 2. 日本語文字列の英語化（文字コードによる構文エラー対策）
replacements = {
    '完了': 'Finished',
    '進行状況': 'Progress',
    'インストールの確認': 'Confirm Installation',
    'ようこそ': 'Welcome',
    'インストール フォルダー': 'Installation Folder',
    '著作権警告': 'Copyright Warning'
}

for ja, en in replacements.items():
    content = content.replace(f'8:{ja}', f'8:{en}')

with open(file_path, 'w', encoding='utf-8') as f:
    f.write(content)

print("Applied fix for Bootstrapper error and Encoding issues (replaced Japanese with English).")
