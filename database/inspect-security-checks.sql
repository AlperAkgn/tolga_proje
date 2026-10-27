-- Step 9: read-only queries. Replace both usernames with the fixture names
-- listed in docs/09-last-run.md. Never select full PasswordHash values.
USE afterform;

SELECT UserName, DisplayName, LENGTH(PasswordHash) AS PasswordHashLength,
       AccessFailedCount, LockoutEnd,
       CASE WHEN LockoutEnd > UTC_TIMESTAMP(6) THEN 1 ELSE 0 END AS IsLockedNow
FROM AspNetUsers
WHERE UserName IN ('qa9.a.RUN', 'qa9.b.RUN');

-- Automatic setup deliberately gives A and B the same random password.
-- Different stored encodings are consistent with per-user random salt.
SELECT a.UserName AS ArtistA, b.UserName AS ArtistB,
       CASE WHEN a.PasswordHash <> b.PasswordHash THEN 1 ELSE 0 END AS HashesDiffer
FROM AspNetUsers AS a
CROSS JOIN AspNetUsers AS b
WHERE a.UserName = 'qa9.a.RUN' AND b.UserName = 'qa9.b.RUN';

SELECT a.UserName, d.Id, d.Title, d.Status, d.FileSizeBytes,
       d.FileSha256, d.SubmittedAtUtc
FROM Demos AS d
JOIN AspNetUsers AS a ON a.Id = d.ArtistId
WHERE a.UserName IN ('qa9.a.RUN', 'qa9.b.RUN')
ORDER BY a.UserName, d.Id;
