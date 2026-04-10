-- Executar no banco OESCRIBA (corrige: Invalid object name 'dbo.ItensPedido').
IF OBJECT_ID(N'dbo.ItensPedido', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ItensPedido
    (
        Id              INT IDENTITY(1, 1) NOT NULL PRIMARY KEY,
        ID_PEDIDO       BIGINT             NULL,
        IdProduto       BIGINT             NULL,
        Codigo          VARCHAR(50)        NULL,
        Descricao       VARCHAR(255)       NULL,
        Unidade         VARCHAR(10)        NULL,
        Quantidade      DECIMAL(10, 2)     NULL,
        ValorUnitario   DECIMAL(10, 4)     NULL
    );
END
GO
