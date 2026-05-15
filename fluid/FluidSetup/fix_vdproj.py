
import re

file_path = 'FluidSetup.vdproj'
with open(file_path, 'r', encoding='utf-8') as f:
    content = f.read()

# 除外対象のファイルパターン
exclude_patterns = [
    r'System\.Memory\.dll',
    r'System\.Buffers\.dll',
    r'System\.Numerics\.Vectors\.dll',
    r'.*\.winmd'
]

# ブロックごとに分割して処理
# { guid }:_id { ... } という構造
blocks = re.split(r'(\s+"\{[A-F0-9-]+\}:_[A-F0-9]+"[\s\n]+\{)', content)

new_content = [blocks[0]]
for i in range(1, len(blocks), 2):
    header = blocks[i]
    body = blocks[i+1]
    
    should_exclude = False
    # SourcePath をチェック
    for pattern in exclude_patterns:
        if re.search(f'"SourcePath" = "8:.*{pattern}"', body):
            should_exclude = True
            break
            
    if should_exclude:
        # Exclude = "11:FALSE" を "11:TRUE" に置換
        body = body.replace('"Exclude" = "11:FALSE"', '"Exclude" = "11:TRUE"')
    
    new_content.append(header)
    new_content.append(body)

result = "".join(new_content)

with open(file_path, 'w', encoding='utf-8') as f:
    f.write(result)
print("Updated vdproj dependencies.")
