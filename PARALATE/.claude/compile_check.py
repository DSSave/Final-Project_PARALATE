# ตรวจว่าสคริปต์ C# ทั้งหมด compile ผ่านโดยไม่ต้องรอ Unity
# วิธีใช้ (จาก root โปรเจกต์):  python .claude/compile_check.py
# หลักการ: คัดลอก Assembly-CSharp.csproj ที่ Unity สร้างไว้ เติมไฟล์ .cs ใหม่ที่ Unity ยังไม่รู้จัก แล้ว dotnet build
import glob, os, re, subprocess, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
os.chdir(ROOT)
BS = chr(92)

src = open('Assembly-CSharp.csproj', encoding='utf-8-sig').read()
have = set(re.findall(r'<Compile Include="([^"]+)"', src))
files = [p.replace('/', BS).replace('&', '&amp;') for p in glob.glob('Assets/Scripts/**/*.cs', recursive=True)]
add = [p for p in files if p not in have]
ins = ''.join('    <Compile Include="%s" />\n' % p for p in add)
src = src.replace('<Compile Include=', ins + '    <Compile Include=', 1)
src = re.sub(r'<OutputPath>[^<]*</OutputPath>', '<OutputPath>Temp/claudecheck/bin/</OutputPath>', src)

proj = 'ClaudeCheck.csproj'
open(proj, 'w', encoding='utf-8').write(src)
try:
    r = subprocess.run(['dotnet', 'build', proj, '-nologo', '-v', 'q'], capture_output=True, text=True)
    lines = sorted({l.strip() for l in r.stdout.splitlines() if 'error' in l.lower() or 'warn' in l.lower()})
    print('\n'.join(lines) or r.stdout[-2000:])
    sys.exit(r.returncode)
finally:
    os.remove(proj)
