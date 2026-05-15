
import re

file_path = 'FluidSetup.vdproj'
with open(file_path, 'r', encoding='utf-8') as f:
    content = f.read()

# 文字化けしたファイル名と、新しい英語名の定義
# 貂帷せ騾夂衍譖ｸbase.docx -> DemeritNoticeBase.docx
content = content.replace('貂帷せ騾夂衍譖ｸbase.docx', 'DemeritNoticeBase.docx')

# ついでに「減点通知書base.docx」という記述がもしあればそれも置換
content = content.replace('減点通知書base.docx', 'DemeritNoticeBase.docx')

with open(file_path, 'w', encoding='utf-8') as f:
    f.write(content)

print("Updated vdproj resource filename (fixed Mojibake).")
