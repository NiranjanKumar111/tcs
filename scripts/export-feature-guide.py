"""Export the offline guide without installing any Python packages."""
from pathlib import Path
from html import escape
import re
from zipfile import ZipFile, ZIP_DEFLATED
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parent.parent
source = root / 'docs' / 'FEATURE_CHANGE_GUIDE.md'
lines = source.read_text(encoding='utf-8').splitlines()
html_parts, paragraphs = [], []
in_code = False
code_lines = []

def inline(text):
    text = escape(text)
    text = re.sub(r'`([^`]+)`', r'<code>\1</code>', text)
    return re.sub(r'\[([^\]]+)\]\((https?://[^)]+)\)', r'<a href="\2">\1</a>', text)

def paragraph(text, style='Normal'):
    paragraphs.append('<w:p><w:pPr><w:pStyle w:val="' + style + '"/></w:pPr>'
        '<w:r><w:t xml:space="preserve">' + escape(text) + '</w:t></w:r></w:p>')

for line in lines:
    if line.startswith('```'):
        if in_code:
            html_parts.append('<pre><code>' + escape('\n'.join(code_lines)) + '</code></pre>')
            for value in code_lines:
                paragraph(value, 'Code')
            code_lines = []
        in_code = not in_code
        continue
    if in_code:
        code_lines.append(line)
        continue
    if not line.strip():
        continue
    match = re.match(r'^(#{1,3}) (.*)', line)
    if match:
        level, title = len(match[1]), match[2]
        html_parts.append(f'<h{level}>{inline(title)}</h{level}>')
        paragraph(title, 'Title' if level == 1 else 'Heading1')
    elif line.startswith('|'):
        if re.match(r'^\|[\s:|\-]+$', line):
            continue
        cells = [c.strip() for c in line.strip('|').split('|')]
        html_parts.append('<div class="table-row">' + ''.join('<span>' + inline(c) + '</span>' for c in cells) + '</div>')
        paragraph('  |  '.join(cells), 'TableText')
    else:
        html_parts.append('<p>' + inline(line) + '</p>')
        paragraph(line)

html_path = source.with_suffix('.html')
html_path.write_text('''<!doctype html><html lang="en"><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Equipment Backend — Beginner Guide</title>
<style>body{font:17px/1.65 system-ui,sans-serif;color:#17293b;max-width:1000px;margin:40px auto;padding:0 25px}
h1{font-size:34px}h2{margin-top:48px;border-bottom:2px solid #c8dae9;padding-bottom:10px}
pre{background:#eff4f8;padding:18px;overflow:auto;font-size:13px;line-height:1.5;white-space:pre-wrap}
code{font-family:Consolas,monospace}.table-row{display:flex;border-bottom:1px solid #ccd8e2;font-size:14px}
.table-row span{flex:1;padding:8px;overflow-wrap:anywhere}a{color:#1268a1}
@media print{body{font-size:10pt;margin:0;max-width:none}h2{break-after:avoid}pre{font-size:8pt}a{color:inherit}}
</style><body><p>Offline edition. Use your browser's Print → Save as PDF to create a PDF copy.</p>'''
    + '\n'.join(html_parts) + '</body></html>', encoding='utf-8')

namespace = 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'
document = '<?xml version="1.0" encoding="UTF-8"?><w:document xmlns:w="' + namespace + '"><w:body>' + ''.join(paragraphs) + '<w:sectPr><w:pgSz w:w="11906" w:h="16838"/><w:pgMar w:top="1000" w:right="1000" w:bottom="1000" w:left="1000"/></w:sectPr></w:body></w:document>'
styles = '<w:styles xmlns:w="' + namespace + '">'
for name, font, size, bold in [('Normal','Calibri',22,False),('Title','Calibri',40,True),('Heading1','Calibri',30,True),('Code','Consolas',17,False),('TableText','Calibri',19,False)]:
    styles += f'<w:style w:type="paragraph" w:styleId="{name}"><w:name w:val="{name}"/><w:pPr><w:spacing w:after="100"/></w:pPr><w:rPr><w:rFonts w:ascii="{font}" w:hAnsi="{font}"/><w:sz w:val="{size}"/>' + ('<w:b/>' if bold else '') + '</w:rPr></w:style>'
styles += '</w:styles>'
docx_path = source.with_suffix('.docx')
with ZipFile(docx_path, 'w', ZIP_DEFLATED) as archive:
    archive.writestr('[Content_Types].xml', '<?xml version="1.0"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/><Override PartName="/word/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/></Types>')
    archive.writestr('_rels/.rels', '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>')
    archive.writestr('word/_rels/document.xml.rels', '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>')
    archive.writestr('word/document.xml', document)
    archive.writestr('word/styles.xml', styles)
with ZipFile(docx_path) as archive:
    assert archive.testzip() is None
    for name in archive.namelist():
        ET.fromstring(archive.read(name))

print('Created and structurally validated FEATURE_CHANGE_GUIDE.docx and HTML.')
