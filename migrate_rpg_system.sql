-- AleaSim RPG & Quest System Migration Script
-- Run this manually in your MySQL workbench or terminal

-- 1. Update PlayerProfiles with RPG Skill columns
ALTER TABLE PlayerProfiles 
ADD COLUMN IF NOT EXISTS LuckyCloverLevel INT DEFAULT 0,
ADD COLUMN IF NOT EXISTS CashbackLevel INT DEFAULT 0,
ADD COLUMN IF NOT EXISTS XpBoostLevel INT DEFAULT 0;

-- 2. Create UserProgressions (Leveling System)
CREATE TABLE IF NOT EXISTS UserProgressions (
    Id CHAR(36) PRIMARY KEY,
    UserId CHAR(36) NOT NULL,
    CurrentLevel INT DEFAULT 1,
    CurrentXP DECIMAL(18,2) DEFAULT 0,
    LifetimeXP DECIMAL(18,2) DEFAULT 0,
    SkillPoints INT DEFAULT 0,
    LastLevelUpAt DATETIME,
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);

-- 3. Create Quests (Global Quest Definitions)
CREATE TABLE IF NOT EXISTS Quests (
    Id CHAR(36) PRIMARY KEY,
    Title VARCHAR(255) NOT NULL,
    Description TEXT,
    GoalType VARCHAR(50) NOT NULL, -- SpinCount, WinAmount, TotalWager
    TargetValue DECIMAL(18,2) NOT NULL,
    RewardAmount DECIMAL(18,2) NOT NULL,
    IsActive BOOLEAN DEFAULT TRUE
);

-- 4. Create UserQuestProgressions (Tracking user progress per quest)
CREATE TABLE IF NOT EXISTS UserQuestProgressions (
    Id CHAR(36) PRIMARY KEY,
    UserId CHAR(36) NOT NULL,
    QuestId CHAR(36) NOT NULL,
    CurrentValue DECIMAL(18,2) DEFAULT 0,
    IsCompleted BOOLEAN DEFAULT FALSE,
    CompletedAt DATETIME,
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
    FOREIGN KEY (QuestId) REFERENCES Quests(Id) ON DELETE CASCADE
);

-- 5. Create Achievements
CREATE TABLE IF NOT EXISTS Achievements (
    Id CHAR(36) PRIMARY KEY,
    Name VARCHAR(255) NOT NULL,
    Description TEXT,
    Icon VARCHAR(50),
    ConditionType VARCHAR(50), -- TotalBets, TotalWagered, etc.
    ConditionValue DECIMAL(18,2)
);

-- 6. Create UserAchievements
CREATE TABLE IF NOT EXISTS UserAchievements (
    Id CHAR(36) PRIMARY KEY,
    UserId CHAR(36) NOT NULL,
    AchievementId CHAR(36) NOT NULL,
    UnlockedAt DATETIME NOT NULL,
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
    FOREIGN KEY (AchievementId) REFERENCES Achievements(Id) ON DELETE CASCADE
);

-- 7. Create Vouchers
CREATE TABLE IF NOT EXISTS Vouchers (
    Id CHAR(36) PRIMARY KEY,
    Code VARCHAR(100) UNIQUE NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    MaxUses INT NOT NULL,
    CurrentUses INT DEFAULT 0,
    ExpiresAt DATETIME,
    CreatedAt DATETIME NOT NULL
);

-- 8. Create UserVouchers
CREATE TABLE IF NOT EXISTS UserVouchers (
    Id CHAR(36) PRIMARY KEY,
    UserId CHAR(36) NOT NULL,
    VoucherId CHAR(36) NOT NULL,
    RedeemedAt DATETIME NOT NULL,
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
    FOREIGN KEY (VoucherId) REFERENCES Vouchers(Id) ON DELETE CASCADE
);

-- 9. Insert Initial Seed Data for Quests (Optional but recommended)
INSERT IGNORE INTO Quests (Id, Title, Description, GoalType, TargetValue, RewardAmount, IsActive) VALUES
('00000000-0000-0000-0000-000000000011', 'Daily Spinner', 'Complete 50 spins on any slot', 'SpinCount', 50.00, 10.00, 1),
('00000000-0000-0000-0000-000000000012', 'High Stakes', 'Wager a total of $1,000', 'TotalWager', 1000.00, 50.00, 1),
('00000000-0000-0000-0000-000000000013', 'Big Win Hunter', 'Win a total of $500', 'WinAmount', 500.00, 25.00, 1);

-- 10. Insert Neon Dice Game
INSERT IGNORE INTO Games (Id, Title, Type, Provider, IsActive, Rtp, MinBet, MaxBet) VALUES
('77777777-7777-7777-7777-777777777777', 'Neon Dice', 'dice', 'AleaSim Originals', 1, 99.0, 0.1, 1000.0);
