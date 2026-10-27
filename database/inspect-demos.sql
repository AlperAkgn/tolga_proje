-- AFTER/FORM step 7: read-only queries for MySQL Workbench.
USE afterform;

SELECT MigrationId, ProductVersion
FROM __EFMigrationsHistory
ORDER BY MigrationId;

SELECT d.Id, a.UserName, d.Title, d.Genre, d.Status,
       d.OriginalFileName, d.ContentType, d.FileSizeBytes,
       d.FileSha256, d.SubmittedAtUtc, d.RightsConfirmedAtUtc
FROM Demos AS d
JOIN AspNetUsers AS a ON a.Id = d.ArtistId
ORDER BY d.SubmittedAtUtc DESC, d.Id DESC;

-- Replace mira.sol with the artist account you want to inspect.
SELECT d.Id, d.Title, d.Status, d.OriginalFileName,
       d.FileSizeBytes, d.FileSha256, d.SubmittedAtUtc
FROM Demos AS d
JOIN AspNetUsers AS a ON a.Id = d.ArtistId
WHERE a.UserName = 'mira.sol'
ORDER BY d.SubmittedAtUtc DESC, d.Id DESC;
