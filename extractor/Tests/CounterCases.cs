using FlexExtractor;
static partial class Cases { public static void TestIdempotentCopyDoesNotInflateCounters(){var c=Cfg(Fresh());Source(c);Scan(c);var r=(ScanResult)Scan(c);Program.Check(r.Copied==0&&r.Skipped==1,"idempotent copy reported as new");} }
