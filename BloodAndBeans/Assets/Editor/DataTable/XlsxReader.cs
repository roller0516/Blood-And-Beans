using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

/// `.xlsx` 한 권을 시트 이름 → 셀 문자열 격자로 읽는다. **에디터 전용이다.**
///
/// 외부 라이브러리를 쓰지 않는다. xlsx는 zip 안에 든 xml이고, 읽을 것은
/// `xl/workbook.xml`(시트 목록) · `xl/_rels/workbook.xml.rels`(시트 파일 경로) ·
/// `xl/sharedStrings.xml`(문자열 풀) · `xl/worksheets/*.xml`(셀) 넷뿐이다.
/// `System.IO.Compression`과 `System.Xml.Linq`는 .NET에 이미 있으므로 의존성이 0이다.
///
/// 수식 칸은 엑셀이 저장해 둔 **계산 결과**를 읽는다. 수식 자체는 다시 계산하지 않는다 —
/// 밸런스 표에 수식을 쓰더라도 엑셀에서 한 번 저장했다면 값이 들어 있다.
public static class XlsxReader
{
    static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    static readonly XNamespace Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    static readonly XNamespace Pkg = "http://schemas.openxmlformats.org/package/2006/relationships";

    /// 시트 한 장. `Rows[행][열]`이고 빈 칸은 빈 문자열이다.
    public sealed class Sheet
    {
        public string Name;

        /// 엑셀에서의 행 번호. `Rows`와 같은 순서로 대응한다.
        public readonly List<int> Lines = new();
        public readonly List<List<string>> Rows = new();
    }

    /// 워크북을 연다. 파일이 없거나 xlsx가 아니면 `error`에 이유를 담고 null을 돌려준다.
    public static Dictionary<string, Sheet> Read(string path, out string error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            error = $"엑셀 파일이 없다: {path}";
            return null;
        }

        try
        {
            using var zip = ZipFile.OpenRead(path);
            var strings = ReadSharedStrings(zip);
            var targets = ReadRelationships(zip);
            var result = new Dictionary<string, Sheet>();

            var workbook = Load(zip, "xl/workbook.xml");
            if (workbook == null)
            {
                error = "xl/workbook.xml이 없다. xlsx 파일이 맞는지 확인해 달라 (.xls는 읽지 못한다).";
                return null;
            }

            foreach (var element in workbook.Descendants(Main + "sheet"))
            {
                var name = (string)element.Attribute("name");
                var id = (string)element.Attribute(Rel + "id");
                if (name == null || id == null || !targets.TryGetValue(id, out var target)) continue;

                var sheet = ReadSheet(zip, Normalize(target), name, strings);
                if (sheet != null) result[name] = sheet;
            }

            if (result.Count == 0) error = "읽을 수 있는 시트가 없다.";
            return result;
        }
        catch (System.Exception e)
        {
            error = $"엑셀을 읽지 못했다: {e.Message}";
            return null;
        }
    }

    /// 워크북 관계 파일의 경로는 `worksheets/sheet1.xml`처럼 `xl/` 기준 상대 경로다.
    static string Normalize(string target) =>
        target.StartsWith("/") ? target.TrimStart('/') :
        target.StartsWith("xl/") ? target : "xl/" + target;

    static XDocument Load(ZipArchive zip, string entryPath)
    {
        var entry = zip.GetEntry(entryPath);
        if (entry == null) return null;

        using var stream = entry.Open();
        return XDocument.Load(stream);
    }

    static Dictionary<string, string> ReadRelationships(ZipArchive zip)
    {
        var map = new Dictionary<string, string>();
        var doc = Load(zip, "xl/_rels/workbook.xml.rels");
        if (doc == null) return map;

        foreach (var element in doc.Descendants(Pkg + "Relationship"))
        {
            var id = (string)element.Attribute("Id");
            var target = (string)element.Attribute("Target");
            if (id != null && target != null) map[id] = target;
        }
        return map;
    }

    /// 문자열 풀. 엑셀은 같은 문자열을 한 번만 저장하고 셀에는 그 번호를 적는다.
    static List<string> ReadSharedStrings(ZipArchive zip)
    {
        var outp = new List<string>();
        var doc = Load(zip, "xl/sharedStrings.xml");
        if (doc == null) return outp;

        foreach (var si in doc.Descendants(Main + "si"))
        {
            // 서식이 섞인 칸은 <t>가 여러 조각으로 나뉜다. 이어 붙여야 원래 문장이 된다.
            var parts = si.Descendants(Main + "t").Select(t => t.Value);
            outp.Add(string.Concat(parts));
        }
        return outp;
    }

    static Sheet ReadSheet(ZipArchive zip, string entryPath, string name, List<string> strings)
    {
        var doc = Load(zip, entryPath);
        if (doc == null) return null;

        var sheet = new Sheet { Name = name };
        foreach (var row in doc.Descendants(Main + "row"))
        {
            var cells = new List<string>();
            foreach (var c in row.Elements(Main + "c"))
            {
                var index = ColumnOf((string)c.Attribute("r"));
                if (index < 0) index = cells.Count;

                // 건너뛴 열은 빈 칸으로 채운다. 그러지 않으면 뒤 열이 앞으로 당겨진다.
                while (cells.Count < index) cells.Add(string.Empty);

                var value = ValueOf(c, strings);
                if (cells.Count == index) cells.Add(value);
                else cells[index] = value;
            }

            if (!int.TryParse((string)row.Attribute("r"), out var line)) line = sheet.Rows.Count + 1;
            sheet.Lines.Add(line);
            sheet.Rows.Add(cells);
        }
        return sheet;
    }

    static string ValueOf(XElement cell, List<string> strings)
    {
        var type = (string)cell.Attribute("t");

        if (type == "inlineStr")
            return string.Concat(cell.Descendants(Main + "t").Select(t => t.Value));

        var v = cell.Element(Main + "v");
        if (v == null) return string.Empty;

        if (type == "s")
            return int.TryParse(v.Value, out var i) && i >= 0 && i < strings.Count ? strings[i] : string.Empty;

        // "str"은 수식의 캐시된 문자열 결과, 타입 없음은 숫자다. 둘 다 값 그대로 쓴다.
        return v.Value;
    }

    /// `"C12"` -> 2. 셀 참조의 앞쪽 알파벳만 본다.
    static int ColumnOf(string reference)
    {
        if (string.IsNullOrEmpty(reference)) return -1;

        var column = 0;
        var seen = false;
        foreach (var ch in reference)
        {
            if (ch >= 'A' && ch <= 'Z') { column = column * 26 + (ch - 'A' + 1); seen = true; }
            else if (ch >= 'a' && ch <= 'z') { column = column * 26 + (ch - 'a' + 1); seen = true; }
            else break;
        }
        return seen ? column - 1 : -1;
    }
}
