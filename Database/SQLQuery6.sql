SELECT TOP 20 Id, EventType, FileName, OldFullPath, DirectoryPath, OccurredAtUtc, ErrorMessage
FROM dbo.FileSystemEvents ORDER BY Id DESC;