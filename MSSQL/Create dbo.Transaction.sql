CREATE TABLE [Transaction] (
    [Txid]      CHAR (64) NOT NULL PRIMARY KEY NONCLUSTERED,
    [BlockHash] CHAR (64) NOT NULL,
    [Version]   TINYINT   NOT NULL,
    [Time]      DATETIME  NOT NULL
);

GO
CREATE CLUSTERED INDEX [CIX_Transaction_Time]
    ON [Transaction]([Time] ASC);