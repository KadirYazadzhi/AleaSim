-- AleaSim Migration V3: Affiliate, Tournaments and System Errors
-- Run this on your MySQL database to fix "Unknown column" errors.

ALTER TABLE Users ADD COLUMN ReferralCode VARCHAR(100) NULL;
ALTER TABLE Users ADD COLUMN ReferredById CHAR(36) NULL;

CREATE TABLE IF NOT EXISTS Tournaments (
    Id CHAR(36) NOT NULL PRIMARY KEY,
    Name VARCHAR(255) NOT NULL,
    Description TEXT NOT NULL,
    StartDate DATETIME(6) NOT NULL,
    EndDate DATETIME(6) NOT NULL,
    PrizePool DECIMAL(18, 2) NOT NULL,
    IsActive TINYINT(1) NOT NULL,
    GameTypesJson TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS SystemErrors (
    Id CHAR(36) NOT NULL PRIMARY KEY,
    Message TEXT NOT NULL,
    StackTrace TEXT NOT NULL,
    Source VARCHAR(255) NOT NULL,
    Path VARCHAR(255) NOT NULL,
    UserId VARCHAR(100) NULL,
    CreatedAt DATETIME(6) NOT NULL
);

UPDATE Users SET ReferralCode = LOWER(Username) WHERE ReferralCode IS NULL;
