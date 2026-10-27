-- AFTER/FORM step 8: read-only queries for MySQL Workbench.
-- Replace mira.sol with the artist username you want to inspect.
USE afterform;

SELECT d.Id, a.UserName, d.Title, d.Genre, d.Status,
       d.SubmittedAtUtc, d.UpdatedAtUtc, d.OriginalFileName, d.FileSizeBytes
FROM Demos AS d
JOIN AspNetUsers AS a ON a.Id = d.ArtistId
WHERE a.UserName = 'mira.sol'
ORDER BY d.SubmittedAtUtc DESC, d.Id DESC
LIMIT 10 OFFSET 0;

-- Account-wide summary; not restricted by the list's search/status filter.
SELECT COUNT(*) AS Total,
       COALESCE(SUM(d.Status = 'Submitted'), 0) AS Submitted,
       COALESCE(SUM(d.Status = 'InReview'), 0) AS InReview,
       COALESCE(SUM(d.Status = 'Accepted'), 0) AS Accepted,
       COALESCE(SUM(d.Status = 'Rejected'), 0) AS Rejected
FROM Demos AS d
JOIN AspNetUsers AS a ON a.Id = d.ArtistId
WHERE a.UserName = 'mira.sol';

-- Example: Submitted demos whose title or genre contains Electronic.
SELECT d.Id, d.Title, d.Genre, d.Status, d.SubmittedAtUtc
FROM Demos AS d
JOIN AspNetUsers AS a ON a.Id = d.ArtistId
WHERE a.UserName = 'mira.sol'
  AND d.Status = 'Submitted'
  AND (d.Title LIKE '%Electronic%' OR d.Genre LIKE '%Electronic%')
ORDER BY d.SubmittedAtUtc DESC, d.Id DESC
LIMIT 10 OFFSET 0;

-- Replace 123 with an ID from this artist's actual list.
SELECT d.Id, d.Title, d.Genre, d.Description, d.Status,
       d.SubmittedAtUtc, d.UpdatedAtUtc, d.OriginalFileName,
       d.ContentType, d.FileSizeBytes, d.FileSha256
FROM Demos AS d
JOIN AspNetUsers AS a ON a.Id = d.ArtistId
WHERE a.UserName = 'mira.sol' AND d.Id = 123;
