SELECT TOP 5 EventType, FileName, ErrorMessage FROM dbo.FileSystemEvents ORDER BY Id DESC;
SELECT COUNT(*) FROM dbo.FileSystemEvents WHERE EventType = 'Created' AND FileName LIKE 'drain%';