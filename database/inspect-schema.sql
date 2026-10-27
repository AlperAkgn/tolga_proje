-- Run these read-only queries in MySQL Workbench after selecting the afterform schema.
USE afterform;

SHOW TABLES;

SELECT MigrationId, ProductVersion
FROM __EFMigrationsHistory
ORDER BY MigrationId;

DESCRIBE AspNetUsers;
DESCRIBE Demos;

-- This foreign key connects every demo to an existing artist account.
SELECT TABLE_NAME, COLUMN_NAME, REFERENCED_TABLE_NAME, REFERENCED_COLUMN_NAME
FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'Demos'
  AND REFERENCED_TABLE_NAME IS NOT NULL;

-- Actual audio submissions appear here after step 7 is activated and used.
SELECT d.Id, a.UserName, a.DisplayName, d.Title, d.Status, d.SubmittedAtUtc
FROM Demos AS d
JOIN AspNetUsers AS a ON a.Id = d.ArtistId
ORDER BY d.SubmittedAtUtc DESC;
