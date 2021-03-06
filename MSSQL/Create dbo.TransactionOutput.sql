﻿CREATE TABLE [TransactionOutput] (
    [Txid]    CHAR (64)       NOT NULL,
    [Index]   SMALLINT        NOT NULL,
    [Address] CHAR (34)       NOT NULL,
    [Value]   DECIMAL (16, 8) NOT NULL,
	PRIMARY KEY CLUSTERED 
	(
		[Txid] ASC,
		[Index] ASC
	)
);

GO
CREATE NONCLUSTERED INDEX [IX_TransactionOutput_Address]
    ON [TransactionOutput]([Address] ASC);