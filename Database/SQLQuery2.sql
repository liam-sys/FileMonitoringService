SELECT TOP 50 * FROM dbo.FileSystemEvents ORDER BY OccurredAtUtc DESC;
SELECT EventType, COUNT(*) AS Total FROM dbo.FileSystemEvents GROUP BY EventType;