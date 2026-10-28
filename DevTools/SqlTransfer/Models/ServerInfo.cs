namespace SqlTransfer.Models;

public sealed record ServerInfo(string ServerName, string ProductVersion, string ProductLevel, string Edition)
{
    public Version? ParsedVersion => Version.TryParse(ProductVersion, out var v) ? v : null;

    public int Major => ParsedVersion?.Major ?? 0;

    public string MarketingName => Major switch
    {
        17 => "SQL Server 2025",
        16 => "SQL Server 2022",
        15 => "SQL Server 2019",
        14 => "SQL Server 2017",
        13 => "SQL Server 2016",
        12 => "SQL Server 2014",
        11 => "SQL Server 2012",
        10 => "SQL Server 2008/2008 R2",
        _ => "SQL Server",
    };

    public string Display => $"{MarketingName} · {ProductVersion} ({ProductLevel}) · {Edition}";
}
