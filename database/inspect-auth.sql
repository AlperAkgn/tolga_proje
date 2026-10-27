-- AFTER/FORM 6. adim: Workbench icin yalnizca okuma sorgulari.
-- PasswordHash'in tamami, salt veya parola ekrana getirilmez.
USE afterform;

-- Kayitli sanatci hesaplarinin guvenli ozeti.
SELECT
    UserName,
    DisplayName,
    CASE
        WHEN PasswordHash IS NOT NULL AND LENGTH(PasswordHash) > 0 THEN 1
        ELSE 0
    END AS HasPasswordHash,
    LENGTH(PasswordHash) AS PasswordHashLength,
    AccessFailedCount,
    LockoutEnd
FROM AspNetUsers
ORDER BY UserName;

-- Yalnizca secilen hesabi incelemek icin kullanici adini degistir.
SELECT
    UserName,
    DisplayName,
    CASE
        WHEN PasswordHash IS NOT NULL AND LENGTH(PasswordHash) > 0 THEN 1
        ELSE 0
    END AS HasPasswordHash,
    LENGTH(PasswordHash) AS PasswordHashLength,
    AccessFailedCount,
    LockoutEnd
FROM AspNetUsers
WHERE UserName = 'mira.sol';
