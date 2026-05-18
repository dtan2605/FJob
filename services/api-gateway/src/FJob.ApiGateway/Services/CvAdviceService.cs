using System.Text;
using System.Text.RegularExpressions;

namespace FJob.ApiGateway.Services;

public sealed class CvAdviceService
{
    private static readonly string[] StopWords =
    [
        "and", "the", "for", "with", "from", "your", "you", "are", "this", "that",
        "have", "has", "will", "can", "using", "use", "need", "needed", "about",
        "trong", "cua", "cho", "voi", "nhung", "cac", "mot", "hai", "nam", "kinh",
        "nghiem", "duoc", "lam", "viec", "ung", "tuyen", "vi", "tri", "tai", "muc"
    ];

    private static readonly Dictionary<string, string[]> SkillAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["python"] = ["python", "django", "flask", "fastapi", "pandas", "numpy"],
        ["javascript"] = ["javascript", "js", "ecmascript"],
        ["typescript"] = ["typescript", "ts"],
        ["react"] = ["react", "nextjs", "next.js"],
        ["angular"] = ["angular"],
        ["vue"] = ["vue", "vuejs", "vue.js"],
        ["nodejs"] = ["node", "nodejs", "node.js", "express", "nestjs"],
        ["dotnet"] = [".net", "dotnet", "asp.net", "c#", "entity framework"],
        ["java"] = ["java", "spring", "spring boot"],
        ["php"] = ["php", "laravel"],
        ["sql"] = ["sql", "mysql", "postgresql", "sql server"],
        ["mongodb"] = ["mongodb", "mongo"],
        ["docker"] = ["docker", "container"],
        ["kubernetes"] = ["kubernetes", "k8s"],
        ["aws"] = ["aws", "amazon web services"],
        ["azure"] = ["azure"],
        ["gcp"] = ["gcp", "google cloud"],
        ["git"] = ["git", "github", "gitlab", "bitbucket"],
        ["rest api"] = ["rest", "restful", "api"],
        ["graphql"] = ["graphql"],
        ["selenium"] = ["selenium"],
        ["testing"] = ["testing", "qa", "tester", "automation test", "unit test"],
        ["linux"] = ["linux", "unix"],
        ["english"] = ["english", "toeic", "ielts"],
        ["excel"] = ["excel"],
        ["figma"] = ["figma"],
        ["communication"] = ["communication", "giao tiep"],
        ["backend"] = ["backend", "back-end"],
        ["frontend"] = ["frontend", "front-end"],
        ["fullstack"] = ["fullstack", "full-stack"],
        ["ai"] = ["ai", "machine learning", "ml", "llm", "deep learning"],
        ["data analysis"] = ["data analysis", "analytics", "phan tich du lieu"]
    };

    public CvAdviceResponse Analyze(CvAdviceRequest request)
    {
        var cvText = request.CvText?.Trim() ?? string.Empty;
        var jobText = string.Join(' ', new[]
        {
            request.JobTitle,
            request.Company,
            request.Location,
            request.Description,
            string.Join(' ', request.Tags ?? Array.Empty<string>())
        }.Where(static value => !string.IsNullOrWhiteSpace(value)));

        var cvTokens = Tokenize(cvText);
        var jobTokens = Tokenize(jobText);
        var cvSkills = ExtractSkills(cvText);
        var jobSkills = ExtractSkills(jobText)
            .Union((request.Tags ?? Array.Empty<string>())
                .Select(tag => tag.Trim())
                .Where(tag => !string.IsNullOrWhiteSpace(tag)), StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var matchedSkills = jobSkills
            .Where(skill => ContainsSkill(cvSkills, skill) || ContainsText(cvText, skill))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static skill => skill)
            .ToArray();

        var missingSkills = jobSkills
            .Where(skill => !ContainsSkill(matchedSkills, skill) && !ContainsText(cvText, skill))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToArray();

        var overlap = jobTokens.Count == 0
            ? 0d
            : jobTokens.Count(token => cvTokens.Contains(token)) / (double)jobTokens.Count;

        var skillCoverage = jobSkills.Length == 0
            ? overlap
            : matchedSkills.Length / (double)jobSkills.Length;

        var matchPercent = (int)Math.Round(Math.Clamp((overlap * 0.45) + (skillCoverage * 0.55), 0d, 1d) * 100d);

        var summary = BuildSummary(matchPercent, matchedSkills, missingSkills, request.JobTitle);
        var strengths = BuildStrengths(matchedSkills, request.JobTitle);
        var improvements = BuildImprovements(missingSkills, matchedSkills, request.JobTitle);

        return new CvAdviceResponse(
            matchPercent,
            summary,
            strengths,
            missingSkills,
            improvements);
    }

    private static string BuildSummary(int matchPercent, string[] matchedSkills, string[] missingSkills, string? jobTitle)
    {
        var role = string.IsNullOrWhiteSpace(jobTitle) ? "vị trí này" : jobTitle.Trim();
        if (matchPercent >= 75)
        {
            return $"CV của bạn đã khá sát với {role}. Hãy tập trung làm nổi bật thành tích và dự án liên quan để tăng sức nặng khi ứng tuyển.";
        }

        if (matchPercent >= 45)
        {
            return $"CV của bạn có nền tảng phù hợp với {role}, nhưng vẫn còn một số khoảng trống kỹ năng hoặc từ khóa quan trọng cần bổ sung rõ hơn.";
        }

        return $"CV hiện chưa bám sát yêu cầu của {role}. Bạn nên chỉnh lại nội dung theo đúng kỹ năng và kinh nghiệm mà công việc này đang ưu tiên.";
    }

    private static string[] BuildStrengths(string[] matchedSkills, string? jobTitle)
    {
        var items = new List<string>();
        if (matchedSkills.Length > 0)
        {
            items.Add($"Bạn đã có tín hiệu khớp với các kỹ năng: {string.Join(", ", matchedSkills.Take(5))}.");
        }

        if (!string.IsNullOrWhiteSpace(jobTitle))
        {
            items.Add($"Tiêu đề CV nên bám gần hơn với vai trò \"{jobTitle.Trim()}\" để tăng độ liên quan khi nhà tuyển dụng quét nhanh.");
        }

        return items.Distinct(StringComparer.OrdinalIgnoreCase).Take(3).ToArray();
    }

    private static string[] BuildImprovements(string[] missingSkills, string[] matchedSkills, string? jobTitle)
    {
        var items = new List<string>();

        foreach (var skill in missingSkills.Take(3))
        {
            items.Add($"Bổ sung dự án, khóa học hoặc thành tích chứng minh năng lực về {skill} ngay trong phần kinh nghiệm hoặc dự án.");
        }

        if (matchedSkills.Length > 0)
        {
            items.Add($"Đưa các kỹ năng đã khớp như {string.Join(", ", matchedSkills.Take(3))} lên phần đầu CV để tăng khả năng qua vòng lọc ATS.");
        }

        items.Add("Viết lại 2-4 bullet kinh nghiệm theo hướng có số liệu, kết quả cụ thể và bám sát mô tả công việc.");

        if (!string.IsNullOrWhiteSpace(jobTitle))
        {
            items.Add($"Tùy biến phần giới thiệu ngắn trong CV theo đúng mục tiêu ứng tuyển cho vị trí {jobTitle.Trim()}.");
        }

        return items.Distinct(StringComparer.OrdinalIgnoreCase).Take(5).ToArray();
    }

    private static HashSet<string> Tokenize(string value)
    {
        var normalized = Normalize(value);
        return normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length >= 2 && !StopWords.Contains(token, StringComparer.OrdinalIgnoreCase))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string[] ExtractSkills(string value)
    {
        var normalized = Normalize(value);
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (skill, aliases) in SkillAliases)
        {
            if (aliases.Any(alias => normalized.Contains(Normalize(alias), StringComparison.OrdinalIgnoreCase)))
            {
                found.Add(skill);
            }
        }

        return found.ToArray();
    }

    private static bool ContainsSkill(IEnumerable<string> skills, string skill) =>
        skills.Contains(skill, StringComparer.OrdinalIgnoreCase);

    private static bool ContainsText(string text, string value) =>
        Normalize(text).Contains(Normalize(value), StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var lowered = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var chars = lowered.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray();
        return Regex.Replace(new string(chars), @"[^a-z0-9+#.\s/-]", " ");
    }
}

public sealed record CvAdviceRequest(
    string CvText,
    string? JobTitle,
    string? Company,
    string? Location,
    string? Description,
    string[]? Tags);

public sealed record CvAdviceResponse(
    int MatchPercent,
    string Summary,
    string[] Strengths,
    string[] MissingSkills,
    string[] Improvements);
