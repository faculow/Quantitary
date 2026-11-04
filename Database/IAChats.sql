-- Ejecutar UNA VEZ en la base correcta, con copia de seguridad previa.
-- No borra ni altera tablas de negocio existentes.
IF OBJECT_ID(N'dbo.IAChats', N'U') IS NULL
BEGIN
  CREATE TABLE dbo.IAChats (
    Id uniqueidentifier NOT NULL CONSTRAINT PK_IAChats PRIMARY KEY,
    UsuarioId int NOT NULL,
    EmpresaId int NOT NULL,
    Titulo nvarchar(120) NOT NULL,
    CreadoEn datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    ActualizadoEn datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
  );
  CREATE INDEX IX_IAChats_Usuario ON dbo.IAChats(UsuarioId, EmpresaId, ActualizadoEn DESC);
END;
IF OBJECT_ID(N'dbo.IAMensajes', N'U') IS NULL
BEGIN
  CREATE TABLE dbo.IAMensajes (
    Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_IAMensajes PRIMARY KEY,
    ChatId uniqueidentifier NOT NULL,
    Rol varchar(16) NOT NULL,
    Texto nvarchar(4000) NOT NULL,
    CreadoEn datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_IAMensajes_IAChats FOREIGN KEY(ChatId) REFERENCES dbo.IAChats(Id) ON DELETE CASCADE,
    CONSTRAINT CK_IAMensajes_Rol CHECK (Rol IN ('user', 'assistant'))
  );
  CREATE INDEX IX_IAMensajes_Chat ON dbo.IAMensajes(ChatId, Id);
END;
IF OBJECT_ID(N'dbo.IAAuditoria', N'U') IS NULL
BEGIN
  CREATE TABLE dbo.IAAuditoria (
    Id bigint IDENTITY(1,1) PRIMARY KEY,
    UsuarioId int NOT NULL, EmpresaId int NOT NULL,
    Accion varchar(30) NOT NULL, ProductoId int NULL,
    Antes nvarchar(1000) NULL, Despues nvarchar(1000) NULL,
    Fecha datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
  );
END;
