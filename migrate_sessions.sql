-- Migration Script for UserSessions
CREATE TABLE IF NOT EXISTS UserSessions (
    Id CHAR(36) PRIMARY KEY,
    UserId CHAR(36) NOT NULL,
    IpAddress VARCHAR(50) NOT NULL,
    UserAgent VARCHAR(500) NOT NULL,
    CreatedAt DATETIME NOT NULL,
    LastActiveAt DATETIME NOT NULL,
    IsActive BOOLEAN NOT NULL DEFAULT 1,
    RefreshToken VARCHAR(500),
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS IX_UserSessions_UserId ON UserSessions(UserId);
CREATE INDEX IF NOT EXISTS IX_UserSessions_RefreshToken ON UserSessions(RefreshToken);
