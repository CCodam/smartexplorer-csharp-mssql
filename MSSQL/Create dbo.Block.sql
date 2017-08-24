CREATE TABLE [Block] (
    [Hash]         CHAR (64)       NOT NULL PRIMARY KEY NONCLUSTERED,
    [Height]       INT             NOT NULL,
    [Confirmation] INT             NOT NULL,
    [Size]         INT             NOT NULL,
    [Difficulty]   DECIMAL (16, 8) NOT NULL,
    [Version]      TINYINT         NOT NULL,
    [Time]         DATETIME        NOT NULL
);

GO
CREATE CLUSTERED INDEX [CIX_Block_Time]
    ON [Block]([Time] ASC);

GO
CREATE NONCLUSTERED INDEX [IX_Block_Height]
    ON [Block]([Height] ASC);