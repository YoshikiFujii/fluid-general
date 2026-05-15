
import re

file_path = 'FluidSetup.vdproj'
with open(file_path, 'r', encoding='utf-8') as f:
    content = f.read()

# 除外対象のファイルパターン
winmd_pattern = r'.*\.winmd'
essential_dlls = [
    r'System\.Memory\.dll',
    r'System\.Buffers\.dll',
    r'System\.Numerics\.Vectors\.dll'
]

# ブロックごとに分割して処理
blocks = re.split(r'(\s+"\{[A-F0-9-]+\}:_[A-F0-9]+"[\s\n]+\{)', content)

new_content = [blocks[0]]
for i in range(1, len(blocks), 2):
    header = blocks[i]
    body = blocks[i+1]
    
    # SourcePath をチェック
    is_winmd = re.search(f'"SourcePath" = "8:.*{winmd_pattern}"', body)
    
    is_essential_dll = False
    for dll in essential_dlls:
        if re.search(f'"SourcePath" = "8:.*{dll}"', body):
            is_essential_dll = True
            break
            
    is_correct_path = '..\\\\fluid\\\\bin\\\\Release\\\\' in body
    
    if is_winmd:
        # winmd は常に除外
        body = body.replace('"Exclude" = "11:FALSE"', '"Exclude" = "11:TRUE"')
    elif is_essential_dll:
        if is_correct_path:
            # bin\Release を指している必須DLLは有効にする
            body = body.replace('"Exclude" = "11:TRUE"', '"Exclude" = "11:FALSE"')
        else:
            # パスが正しくない（FluidSetupフォルダ等を探している）重複項目は除外
            body = body.replace('"Exclude" = "11:FALSE"', '"Exclude" = "11:TRUE"')
    
    new_content.append(header)
    new_content.append(body)

result = "".join(new_content)

with open(file_path, 'w', encoding='utf-8') as f:
    f.write(result)
print("Optimized vdproj dependencies (restored essential DLLs).")
