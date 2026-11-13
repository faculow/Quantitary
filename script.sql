USE [master]
GO

IF DB_ID(N'Quantitary') IS NULL
BEGIN
    CREATE DATABASE [Quantitary];
END
GO

USE [Quantitary]
GO

-- ============================================================
-- ESQUEMA NORMALIZADO Y MULTI-TENANT
-- ============================================================
-- Objetivo:
-- 1) Cada dato vive en una sola tabla de origen.
-- 2) El precio vigente del producto vive en Productos.
-- 3) El stock actual vive en Stock.
-- 4) Los datos históricos de la venta quedan en DetalleVenta.
-- 5) Todos los campos descriptivos que se relacionan con FK viven como FK.
-- ============================================================

-- 1. Empresa
IF OBJECT_ID(N'dbo.Empresa', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Empresa](
        [id] [int] IDENTITY(1,1) NOT NULL,
        [nombre] [varchar](150) NOT NULL,
        CONSTRAINT [PK_Empresa] PRIMARY KEY CLUSTERED ([id] ASC)
    ) ON [PRIMARY];
END;
GO

-- 2. Categoria (maestro por empresa)
IF OBJECT_ID(N'dbo.Categoria', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Categoria](
        [id] [int] IDENTITY(1,1) NOT NULL,
        [empresaID] [int] NOT NULL,
        [nombre] [varchar](100) NOT NULL,
        CONSTRAINT [PK_Categoria] PRIMARY KEY CLUSTERED ([id] ASC),
        CONSTRAINT [UQ_Categoria_Empresa_Nombre] UNIQUE NONCLUSTERED ([empresaID], [nombre])
    ) ON [PRIMARY];
END;
GO

-- 3. Proveedores (maestro por empresa)
IF OBJECT_ID(N'dbo.Proveedores', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Proveedores](
        [id] [int] IDENTITY(1,1) NOT NULL,
        [empresaID] [int] NOT NULL,
        [nombre] [varchar](150) NOT NULL,
        [telefono] [varchar](30) NULL,
        [mail] [varchar](150) NULL,
        [costoEnvio] [decimal](12, 2) NOT NULL DEFAULT ((0.00)),
        [detalles] [varchar](max) NULL,
        CONSTRAINT [PK_Proveedores] PRIMARY KEY CLUSTERED ([id] ASC),
        CONSTRAINT [UQ_Proveedores_Empresa_Nombre] UNIQUE NONCLUSTERED ([empresaID], [nombre])
    ) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY];
END;
GO

-- 4. Permisos
IF OBJECT_ID(N'dbo.Permisos', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Permisos](
        [id] [int] IDENTITY(1,1) NOT NULL,
        [registrarVentas] [bit] NOT NULL DEFAULT ((0)),
        [verHistorialVentas] [bit] NOT NULL DEFAULT ((0)),
        [anularVentas] [bit] NOT NULL DEFAULT ((0)),
        [verProductos] [bit] NOT NULL DEFAULT ((0)),
        [crearProductos] [bit] NOT NULL DEFAULT ((0)),
        [editarProductos] [bit] NOT NULL DEFAULT ((0)),
        [eliminarProductos] [bit] NOT NULL DEFAULT ((0)),
        [verStock] [bit] NOT NULL DEFAULT ((0)),
        [verDashboard] [bit] NOT NULL DEFAULT ((0)),
        [gestionarUsuarios] [bit] NOT NULL DEFAULT ((0)),
        [verGanacia] [bit] NOT NULL DEFAULT ((0)),
        CONSTRAINT [PK_Permisos] PRIMARY KEY CLUSTERED ([id] ASC)
    ) ON [PRIMARY];
END;
GO

-- 5. Rol
IF OBJECT_ID(N'dbo.Rol', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Rol](
        [id] [int] IDENTITY(1,1) NOT NULL,
        [empresaID] [int] NULL,
        [nombre] [varchar](100) NOT NULL,
        [permisoID] [int] NOT NULL,
        CONSTRAINT [PK_Rol] PRIMARY KEY CLUSTERED ([id] ASC),
        CONSTRAINT [UQ_Rol_Empresa_Nombre] UNIQUE NONCLUSTERED ([empresaID], [nombre])
    ) ON [PRIMARY];
END;
GO

-- Compatibilidad con bases anteriores, donde Rol ya existia sin empresaID.
-- La columna se deja nullable para no invalidar los roles existentes.
IF COL_LENGTH(N'dbo.Rol', N'empresaID') IS NULL
BEGIN
    ALTER TABLE [dbo].[Rol]
        ADD [empresaID] [int] NULL;
END;
GO

-- 6. Usuarios
IF OBJECT_ID(N'dbo.Usuarios', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Usuarios](
        [id] [int] IDENTITY(1,1) NOT NULL,
        [nombreUsuario] [varchar](100) NOT NULL,
        [contrasena] [varchar](255) NOT NULL,
        [mail] [varchar](150) NOT NULL,
        [numeroTelefono] [varchar](30) NULL,
        [empresaID] [int] NOT NULL,
        [rolID] [int] NOT NULL,
        CONSTRAINT [PK_Usuarios] PRIMARY KEY CLUSTERED ([id] ASC),
        CONSTRAINT [UQ_Usuarios_Empresa_Mail] UNIQUE NONCLUSTERED ([empresaID], [mail])
    ) ON [PRIMARY];
END;
GO

-- 7. Productos
-- Fuente de verdad del catálogo, precios vigentes y atributos del producto.
-- No se guarda stock ni nombre de producto en otras tablas, porque se obtienen por JOIN.
IF OBJECT_ID(N'dbo.Productos', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Productos](
        [id] [int] IDENTITY(1,1) NOT NULL,
        [empresaID] [int] NOT NULL,
        [categoriaID] [int] NOT NULL,
        [proveedorID] [int] NULL,
        [nombre] [varchar](150) NOT NULL,
        [sku] [varchar](100) NOT NULL,
        [descripcion] [varchar](500) NULL,
        [fechaCarga] [datetime2](0) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [precioVenta] [decimal](12, 2) NOT NULL DEFAULT ((0.00)),
        [costoCompra] [decimal](12, 2) NOT NULL DEFAULT ((0.00)),
        [activo] [bit] NOT NULL DEFAULT ((1)),
        CONSTRAINT [PK_Productos] PRIMARY KEY CLUSTERED ([id] ASC),
        CONSTRAINT [UQ_Productos_Empresa_Sku] UNIQUE NONCLUSTERED ([empresaID], [sku]),
        CONSTRAINT [CK_Productos_PrecioVenta_Positive] CHECK ([precioVenta] >= 0),
        CONSTRAINT [CK_Productos_CostoCompra_Positive] CHECK ([costoCompra] >= 0)
    ) ON [PRIMARY];
END;
GO

-- 8. Stock
-- Fuente de verdad del inventario actual por producto.
-- La cantidad, no el nombre ni el precio, pertenece a Stock.
IF OBJECT_ID(N'dbo.Stock', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Stock](
        [id] [int] IDENTITY(1,1) NOT NULL,
        [productoID] [int] NOT NULL,
        [cantidad] [int] NOT NULL DEFAULT ((0)),
        [cantidadMinimoAlerta] [int] NOT NULL DEFAULT ((0)),
        [activo] [bit] NOT NULL DEFAULT ((1)),
        [actualizadoEn] [datetime2](0) NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_Stock] PRIMARY KEY CLUSTERED ([id] ASC),
        CONSTRAINT [CK_Stock_Cantidad_Positive] CHECK ([cantidad] >= 0),
        CONSTRAINT [CK_Stock_Minimo_Positive] CHECK ([cantidadMinimoAlerta] >= 0)
    ) ON [PRIMARY];
END;
GO

-- 9. Ventas (cabecera del comprobante)
-- Solo guarda datos generales del comprobante; no replica detalles del producto.
IF OBJECT_ID(N'dbo.Ventas', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Ventas](
        [id] [int] IDENTITY(1,1) NOT NULL,
        [empresaID] [int] NOT NULL,
        [usuarioID] [int] NOT NULL,
        [fecha] [datetime2](0) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [metodoPago] [varchar](50) NOT NULL,
        [totalVenta] [decimal](12, 2) NOT NULL DEFAULT ((0.00)),
        [observacion] [varchar](500) NULL,
        CONSTRAINT [PK_Ventas] PRIMARY KEY CLUSTERED ([id] ASC),
        CONSTRAINT [CK_Ventas_Total_Positive] CHECK ([totalVenta] >= 0)
    ) ON [PRIMARY];
END;
GO

-- 10. DetalleVenta (snapshot histórico de cada línea vendida)
-- Aquí vive el precio y costo exactos al momento de cerrar la venta.
IF OBJECT_ID(N'dbo.DetalleVenta', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[DetalleVenta](
        [id] [int] IDENTITY(1,1) NOT NULL,
        [ventaID] [int] NOT NULL,
        [productoID] [int] NOT NULL,
        [cantidad] [int] NOT NULL,
        [precioVenta] [decimal](12, 2) NOT NULL,
        [costoCompra] [decimal](12, 2) NOT NULL DEFAULT ((0.00)),
        [porcentajeGanancia] [decimal](10, 2) NULL,
        [ganancia] [decimal](12, 2) NOT NULL DEFAULT ((0.00)),
        CONSTRAINT [PK_DetalleVenta] PRIMARY KEY CLUSTERED ([id] ASC),
        CONSTRAINT [CK_DetalleVenta_Cantidad_Positive] CHECK ([cantidad] > 0),
        CONSTRAINT [CK_DetalleVenta_Precio_Positive] CHECK ([precioVenta] >= 0),
        CONSTRAINT [CK_DetalleVenta_Costo_Positive] CHECK ([costoCompra] >= 0),
        CONSTRAINT [UQ_DetalleVenta_Venta_Producto] UNIQUE NONCLUSTERED ([ventaID], [productoID])
    ) ON [PRIMARY];
END;
GO

-- Ajuste para bases ya existentes: consolidar duplicados y crear unicidad por venta+producto
DELETE d
FROM dbo.DetalleVenta d
WHERE d.ventaID IS NULL
   OR d.productoID IS NULL;
GO

IF OBJECT_ID('tempdb..#DuplicadosDetalleVenta') IS NOT NULL
    DROP TABLE #DuplicadosDetalleVenta;

SELECT
    MIN(id) AS idConservar,
    ventaID,
    productoID,
    SUM(ISNULL(cantidad, 0)) AS cantidadTotal,
    SUM(ISNULL(precioVenta, 0)) AS precioVentaTotal,
    SUM(ISNULL(costoCompra, 0)) AS costoCompraTotal,
    SUM(ISNULL(ganancia, 0)) AS gananciaTotal,
    COUNT(*) AS cantidadFilas
INTO #DuplicadosDetalleVenta
FROM dbo.DetalleVenta
GROUP BY ventaID, productoID
HAVING COUNT(*) > 1;

UPDATE d
SET d.cantidad = x.cantidadTotal,
    d.precioVenta = x.precioVentaTotal,
    d.costoCompra = x.costoCompraTotal,
    d.ganancia = x.gananciaTotal,
    d.porcentajeGanancia = CASE
        WHEN x.precioVentaTotal = 0 THEN NULL
        ELSE ROUND((x.gananciaTotal / x.precioVentaTotal) * 100, 2)
    END
FROM dbo.DetalleVenta d
INNER JOIN #DuplicadosDetalleVenta x ON x.idConservar = d.id;

DELETE d
FROM dbo.DetalleVenta d
INNER JOIN #DuplicadosDetalleVenta x
    ON x.ventaID = d.ventaID
   AND x.productoID = d.productoID
WHERE d.id <> x.idConservar;

DROP TABLE #DuplicadosDetalleVenta;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'UQ_DetalleVenta_Venta_Producto'
      AND object_id = OBJECT_ID('dbo.DetalleVenta')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UQ_DetalleVenta_Venta_Producto
        ON dbo.DetalleVenta (ventaID, productoID);
END;
GO

-- ============================================================
-- QUANTITARY IA: CHATS, MENSAJES Y AUDITORIA
-- ============================================================
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

    CREATE INDEX IX_IAChats_Usuario
        ON dbo.IAChats(UsuarioId, EmpresaId, ActualizadoEn DESC);
END;
GO

IF OBJECT_ID(N'dbo.IAMensajes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.IAMensajes (
        Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_IAMensajes PRIMARY KEY,
        ChatId uniqueidentifier NOT NULL,
        Rol varchar(16) NOT NULL,
        Texto nvarchar(4000) NOT NULL,
        ContenidoJson nvarchar(max) NULL,
        CreadoEn datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_IAMensajes_IAChats
            FOREIGN KEY(ChatId) REFERENCES dbo.IAChats(Id) ON DELETE CASCADE,
        CONSTRAINT CK_IAMensajes_Rol
            CHECK (Rol IN ('user', 'assistant'))
    );

    CREATE INDEX IX_IAMensajes_Chat
        ON dbo.IAMensajes(ChatId, Id);
END;
GO

-- Compatibilidad con chats creados antes de persistir tablas, gráficos y acciones.
IF COL_LENGTH(N'dbo.IAMensajes', N'ContenidoJson') IS NULL
BEGIN
    ALTER TABLE dbo.IAMensajes
        ADD ContenidoJson nvarchar(max) NULL;
END;
GO

IF OBJECT_ID(N'dbo.IAAuditoria', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.IAAuditoria (
        Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_IAAuditoria PRIMARY KEY,
        UsuarioId int NOT NULL,
        EmpresaId int NOT NULL,
        Accion varchar(30) NOT NULL,
        ProductoId int NULL,
        Antes nvarchar(1000) NULL,
        Despues nvarchar(1000) NULL,
        Fecha datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END;
GO

-- ============================================================
-- FKs: integridad referencial explícita
-- ============================================================

-- Reemplazo forzado de FKs conflictivas por rutas múltiples de cascade
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Productos_Categoria')
    ALTER TABLE [dbo].[Productos] DROP CONSTRAINT [FK_Productos_Categoria];
GO
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Productos_Proveedor')
    ALTER TABLE [dbo].[Productos] DROP CONSTRAINT [FK_Productos_Proveedor];
GO
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Ventas_Usuarios')
    ALTER TABLE [dbo].[Ventas] DROP CONSTRAINT [FK_Ventas_Usuarios];
GO
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_DetalleVenta_Productos')
    ALTER TABLE [dbo].[DetalleVenta] DROP CONSTRAINT [FK_DetalleVenta_Productos];
GO

-- Empresa relacionada
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Categoria_Empresa')
BEGIN
    ALTER TABLE [dbo].[Categoria] WITH CHECK ADD CONSTRAINT [FK_Categoria_Empresa]
        FOREIGN KEY([empresaID]) REFERENCES [dbo].[Empresa] ([id])
        ON UPDATE NO ACTION ON DELETE NO ACTION;
END;
GO
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Categoria_Empresa')
    ALTER TABLE [dbo].[Categoria] CHECK CONSTRAINT [FK_Categoria_Empresa];
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Proveedores_Empresa')
BEGIN
    ALTER TABLE [dbo].[Proveedores] WITH CHECK ADD CONSTRAINT [FK_Proveedores_Empresa]
        FOREIGN KEY([empresaID]) REFERENCES [dbo].[Empresa] ([id])
        ON UPDATE NO ACTION ON DELETE NO ACTION;
END;
GO
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Proveedores_Empresa')
    ALTER TABLE [dbo].[Proveedores] CHECK CONSTRAINT [FK_Proveedores_Empresa];
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Rol_Permisos')
BEGIN
    ALTER TABLE [dbo].[Rol] WITH CHECK ADD CONSTRAINT [FK_Rol_Permisos]
        FOREIGN KEY([permisoID]) REFERENCES [dbo].[Permisos] ([id])
        ON UPDATE NO ACTION ON DELETE NO ACTION;
END;
GO
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Rol_Permisos')
    ALTER TABLE [dbo].[Rol] CHECK CONSTRAINT [FK_Rol_Permisos];
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Usuarios_Empresa')
BEGIN
    ALTER TABLE [dbo].[Usuarios] WITH CHECK ADD CONSTRAINT [FK_Usuarios_Empresa]
        FOREIGN KEY([empresaID]) REFERENCES [dbo].[Empresa] ([id])
        ON UPDATE NO ACTION ON DELETE NO ACTION;
END;
GO
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Usuarios_Empresa')
    ALTER TABLE [dbo].[Usuarios] CHECK CONSTRAINT [FK_Usuarios_Empresa];
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Usuarios_Rol')
BEGIN
    ALTER TABLE [dbo].[Usuarios] WITH CHECK ADD CONSTRAINT [FK_Usuarios_Rol]
        FOREIGN KEY([rolID]) REFERENCES [dbo].[Rol] ([id])
        ON UPDATE NO ACTION ON DELETE NO ACTION;
END;
GO
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Usuarios_Rol')
    ALTER TABLE [dbo].[Usuarios] CHECK CONSTRAINT [FK_Usuarios_Rol];
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Productos_Empresa')
BEGIN
    ALTER TABLE [dbo].[Productos] WITH CHECK ADD CONSTRAINT [FK_Productos_Empresa]
        FOREIGN KEY([empresaID]) REFERENCES [dbo].[Empresa] ([id])
        ON UPDATE NO ACTION ON DELETE NO ACTION;
END;
GO
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Productos_Empresa')
    ALTER TABLE [dbo].[Productos] CHECK CONSTRAINT [FK_Productos_Empresa];
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Productos_Categoria')
BEGIN
    ALTER TABLE [dbo].[Productos] WITH CHECK ADD CONSTRAINT [FK_Productos_Categoria]
        FOREIGN KEY([categoriaID]) REFERENCES [dbo].[Categoria] ([id])
        ON UPDATE NO ACTION ON DELETE NO ACTION;
END;
GO
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Productos_Categoria')
    ALTER TABLE [dbo].[Productos] CHECK CONSTRAINT [FK_Productos_Categoria];
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Productos_Proveedor')
BEGIN
    ALTER TABLE [dbo].[Productos] WITH CHECK ADD CONSTRAINT [FK_Productos_Proveedor]
        FOREIGN KEY([proveedorID]) REFERENCES [dbo].[Proveedores] ([id])
        ON UPDATE NO ACTION ON DELETE SET NULL;
END;
GO
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Productos_Proveedor')
    ALTER TABLE [dbo].[Productos] CHECK CONSTRAINT [FK_Productos_Proveedor];
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Stock_Productos')
BEGIN
    ALTER TABLE [dbo].[Stock] WITH CHECK ADD CONSTRAINT [FK_Stock_Productos]
        FOREIGN KEY([productoID]) REFERENCES [dbo].[Productos] ([id])
        ON UPDATE NO ACTION ON DELETE CASCADE;
END;
GO
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Stock_Productos')
    ALTER TABLE [dbo].[Stock] CHECK CONSTRAINT [FK_Stock_Productos];
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Ventas_Empresa')
BEGIN
    ALTER TABLE [dbo].[Ventas] WITH CHECK ADD CONSTRAINT [FK_Ventas_Empresa]
        FOREIGN KEY([empresaID]) REFERENCES [dbo].[Empresa] ([id])
        ON UPDATE NO ACTION ON DELETE NO ACTION;
END;
GO
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Ventas_Empresa')
    ALTER TABLE [dbo].[Ventas] CHECK CONSTRAINT [FK_Ventas_Empresa];
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Ventas_Usuarios')
BEGIN
    ALTER TABLE [dbo].[Ventas] WITH CHECK ADD CONSTRAINT [FK_Ventas_Usuarios]
        FOREIGN KEY([usuarioID]) REFERENCES [dbo].[Usuarios] ([id])
        ON UPDATE NO ACTION ON DELETE NO ACTION;
END;
GO
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Ventas_Usuarios')
    ALTER TABLE [dbo].[Ventas] CHECK CONSTRAINT [FK_Ventas_Usuarios];
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_DetalleVenta_Ventas')
BEGIN
    ALTER TABLE [dbo].[DetalleVenta] WITH CHECK ADD CONSTRAINT [FK_DetalleVenta_Ventas]
        FOREIGN KEY([ventaID]) REFERENCES [dbo].[Ventas] ([id])
        ON UPDATE NO ACTION ON DELETE CASCADE;
END;
GO
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_DetalleVenta_Ventas')
    ALTER TABLE [dbo].[DetalleVenta] CHECK CONSTRAINT [FK_DetalleVenta_Ventas];
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_DetalleVenta_Productos')
BEGIN
    ALTER TABLE [dbo].[DetalleVenta] WITH CHECK ADD CONSTRAINT [FK_DetalleVenta_Productos]
        FOREIGN KEY([productoID]) REFERENCES [dbo].[Productos] ([id])
        ON UPDATE NO ACTION ON DELETE NO ACTION;
END;
GO
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_DetalleVenta_Productos')
    ALTER TABLE [dbo].[DetalleVenta] CHECK CONSTRAINT [FK_DetalleVenta_Productos];
GO

-- ============================================================
-- DATOS INICIALES
-- ============================================================

DECLARE @EmpresaId int;
DECLARE @PermisosId int;
DECLARE @RolId int;
DECLARE @UsuarioId int;

IF NOT EXISTS (SELECT 1 FROM [dbo].[Empresa] WHERE [nombre] = 'Quantitary Admin')
BEGIN
    INSERT INTO [dbo].[Empresa] ([nombre]) VALUES ('Quantitary Admin');
END;
SET @EmpresaId = (SELECT TOP 1 [id] FROM [dbo].[Empresa] WHERE [nombre] = 'Quantitary Admin' ORDER BY [id]);

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permisos])
BEGIN
    INSERT INTO [dbo].[Permisos]
        ([registrarVentas], [verHistorialVentas], [anularVentas], [verProductos], [crearProductos], [editarProductos], [eliminarProductos], [verStock], [verDashboard], [gestionarUsuarios], [verGanacia])
    VALUES
        (1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1);
END;
SET @PermisosId = (SELECT TOP 1 [id] FROM [dbo].[Permisos] ORDER BY [id]);

-- Si la base viene del esquema anterior, asociar el Admin existente a la
-- empresa inicial para evitar crear un rol duplicado.
UPDATE [dbo].[Rol]
SET [empresaID] = @EmpresaId
WHERE [empresaID] IS NULL
  AND [nombre] = 'Admin';

IF NOT EXISTS (SELECT 1 FROM [dbo].[Categoria] WHERE [empresaID] = @EmpresaId AND [nombre] = 'Electrónica')
BEGIN
    INSERT INTO [dbo].[Categoria] ([empresaID], [nombre])
    VALUES (@EmpresaId, 'Electrónica');
END;

IF NOT EXISTS (SELECT 1 FROM [dbo].[Categoria] WHERE [empresaID] = @EmpresaId AND [nombre] = 'Accesorios')
BEGIN
    INSERT INTO [dbo].[Categoria] ([empresaID], [nombre])
    VALUES (@EmpresaId, 'Accesorios');
END;

IF NOT EXISTS (SELECT 1 FROM [dbo].[Categoria] WHERE [empresaID] = @EmpresaId AND [nombre] = 'Computación')
BEGIN
    INSERT INTO [dbo].[Categoria] ([empresaID], [nombre])
    VALUES (@EmpresaId, 'Computación');
END;

IF NOT EXISTS (SELECT 1 FROM [dbo].[Proveedores] WHERE [empresaID] = @EmpresaId AND [nombre] = 'TechSupply')
BEGIN
    INSERT INTO [dbo].[Proveedores] ([empresaID], [nombre], [telefono], [mail], [costoEnvio], [detalles])
    VALUES (@EmpresaId, 'TechSupply', '5491123456789', 'ventas@techsupply.com', 1500.00, 'Proveedor principal de equipamiento tecnológico');
END;

IF NOT EXISTS (SELECT 1 FROM [dbo].[Proveedores] WHERE [empresaID] = @EmpresaId AND [nombre] = 'GlobalParts')
BEGIN
    INSERT INTO [dbo].[Proveedores] ([empresaID], [nombre], [telefono], [mail], [costoEnvio], [detalles])
    VALUES (@EmpresaId, 'GlobalParts', '5491187654321', 'info@globalparts.com', 1000.00, 'Proveedor de accesorios y periféricos');
END;

IF NOT EXISTS (SELECT 1 FROM [dbo].[Rol] WHERE [empresaID] = @EmpresaId AND [nombre] = 'Admin')
BEGIN
    INSERT INTO [dbo].[Rol] ([empresaID], [nombre], [permisoID])
    VALUES (@EmpresaId, 'Admin', @PermisosId);
END;
SET @RolId = (SELECT TOP 1 [id] FROM [dbo].[Rol] WHERE [empresaID] = @EmpresaId AND [nombre] = 'Admin' ORDER BY [id]);

IF NOT EXISTS (SELECT 1 FROM [dbo].[Usuarios] WHERE [empresaID] = @EmpresaId AND [mail] = 'admin@quantitary.com')
BEGIN
    INSERT INTO [dbo].[Usuarios] ([nombreUsuario], [contrasena], [mail], [numeroTelefono], [empresaID], [rolID])
    VALUES ('admin', 'Abc1234', 'admin@quantitary.com', '5491123456789', @EmpresaId, @RolId);
END;
SET @UsuarioId = (SELECT TOP 1 [id] FROM [dbo].[Usuarios] WHERE [empresaID] = @EmpresaId AND [mail] = 'admin@quantitary.com' ORDER BY [id]);

IF NOT EXISTS (SELECT 1 FROM [dbo].[Productos] WHERE [empresaID] = @EmpresaId AND [sku] = 'N-001')
BEGIN
    INSERT INTO [dbo].[Productos] ([empresaID], [categoriaID], [proveedorID], [nombre], [sku], [descripcion], [fechaCarga], [precioVenta], [costoCompra])
    VALUES
        (@EmpresaId,
         (SELECT TOP 1 [id] FROM [dbo].[Categoria] WHERE [empresaID] = @EmpresaId AND [nombre] = 'Electrónica'),
         (SELECT TOP 1 [id] FROM [dbo].[Proveedores] WHERE [empresaID] = @EmpresaId AND [nombre] = 'TechSupply'),
         'Notebook Dell Inspiron 15', 'N-001', 'Notebook de uso general para oficina y estudio', SYSUTCDATETIME(), 890000.00, 600000.00),
        (@EmpresaId,
         (SELECT TOP 1 [id] FROM [dbo].[Categoria] WHERE [empresaID] = @EmpresaId AND [nombre] = 'Accesorios'),
         (SELECT TOP 1 [id] FROM [dbo].[Proveedores] WHERE [empresaID] = @EmpresaId AND [nombre] = 'GlobalParts'),
         'Mouse Logitech G203', 'M-010', 'Mouse gamer RGB con 6 botones', SYSUTCDATETIME(), 38000.00, 18000.00),
        (@EmpresaId,
         (SELECT TOP 1 [id] FROM [dbo].[Categoria] WHERE [empresaID] = @EmpresaId AND [nombre] = 'Accesorios'),
         (SELECT TOP 1 [id] FROM [dbo].[Proveedores] WHERE [empresaID] = @EmpresaId AND [nombre] = 'GlobalParts'),
         'Teclado Mecánico Redragon', 'K-205', 'Teclado mecánico con switches rojos', SYSUTCDATETIME(), 62000.00, 29000.00),
        (@EmpresaId,
         (SELECT TOP 1 [id] FROM [dbo].[Categoria] WHERE [empresaID] = @EmpresaId AND [nombre] = 'Computación'),
         (SELECT TOP 1 [id] FROM [dbo].[Proveedores] WHERE [empresaID] = @EmpresaId AND [nombre] = 'TechSupply'),
         'Monitor Samsung 24"', 'MO-120', 'Monitor Full HD 24 pulgadas', SYSUTCDATETIME(), 132000.00, 85000.00),
        (@EmpresaId,
         (SELECT TOP 1 [id] FROM [dbo].[Categoria] WHERE [empresaID] = @EmpresaId AND [nombre] = 'Electrónica'),
         (SELECT TOP 1 [id] FROM [dbo].[Proveedores] WHERE [empresaID] = @EmpresaId AND [nombre] = 'TechSupply'),
         'Cámara Logitech C920', 'C-300', 'Webcam Full HD para reuniones y streaming', SYSUTCDATETIME(), 84000.00, 48000.00);
END;

IF NOT EXISTS (SELECT 1 FROM [dbo].[Stock] s INNER JOIN [dbo].[Productos] p ON p.id = s.productoID WHERE p.empresaID = @EmpresaId AND p.sku = 'N-001')
BEGIN
    INSERT INTO [dbo].[Stock] ([productoID], [cantidad], [cantidadMinimoAlerta], [activo])
    VALUES
        ((SELECT TOP 1 [id] FROM [dbo].[Productos] WHERE [empresaID] = @EmpresaId AND [sku] = 'N-001'), 15, 5, 1),
        ((SELECT TOP 1 [id] FROM [dbo].[Productos] WHERE [empresaID] = @EmpresaId AND [sku] = 'M-010'), 40, 10, 1),
        ((SELECT TOP 1 [id] FROM [dbo].[Productos] WHERE [empresaID] = @EmpresaId AND [sku] = 'K-205'), 20, 8, 1),
        ((SELECT TOP 1 [id] FROM [dbo].[Productos] WHERE [empresaID] = @EmpresaId AND [sku] = 'MO-120'), 12, 4, 1),
        ((SELECT TOP 1 [id] FROM [dbo].[Productos] WHERE [empresaID] = @EmpresaId AND [sku] = 'C-300'), 18, 6, 1);
END;

IF NOT EXISTS (SELECT 1 FROM [dbo].[Ventas] WHERE [empresaID] = @EmpresaId AND [usuarioID] = @UsuarioId)
BEGIN
    INSERT INTO [dbo].[Ventas] ([empresaID], [usuarioID], [fecha], [metodoPago], [totalVenta], [observacion])
    VALUES
        (@EmpresaId, @UsuarioId, SYSUTCDATETIME(), 'Tarjeta', 890000.00, 'Venta inicial de prueba'),
        (@EmpresaId, @UsuarioId, DATEADD(day, 7, SYSUTCDATETIME()), 'Efectivo', 76000.00, 'Venta inicial de prueba'),
        (@EmpresaId, @UsuarioId, DATEADD(day, 15, SYSUTCDATETIME()), 'Transferencia', 62000.00, 'Venta inicial de prueba');
END;

IF NOT EXISTS (SELECT 1 FROM [dbo].[DetalleVenta] dv
               INNER JOIN [dbo].[Ventas] v ON v.id = dv.ventaID
               WHERE v.empresaID = @EmpresaId AND dv.productoID = (SELECT TOP 1 [id] FROM [dbo].[Productos] WHERE [empresaID] = @EmpresaId AND [sku] = 'N-001'))
BEGIN
    INSERT INTO [dbo].[DetalleVenta] ([ventaID], [productoID], [cantidad], [precioVenta], [costoCompra], [porcentajeGanancia], [ganancia])
    VALUES
        ((SELECT TOP 1 [id] FROM [dbo].[Ventas] WHERE [empresaID] = @EmpresaId AND [usuarioID] = @UsuarioId ORDER BY [fecha] ASC),
         (SELECT TOP 1 [id] FROM [dbo].[Productos] WHERE [empresaID] = @EmpresaId AND [sku] = 'N-001'),
         1, 890000.00, 600000.00, 48.33, 290000.00),
        ((SELECT TOP 1 [id] FROM [dbo].[Ventas] WHERE [empresaID] = @EmpresaId AND [usuarioID] = @UsuarioId ORDER BY [fecha] DESC),
         (SELECT TOP 1 [id] FROM [dbo].[Productos] WHERE [empresaID] = @EmpresaId AND [sku] = 'M-010'),
         2, 76000.00, 36000.00, 111.11, 40000.00),
        ((SELECT TOP 1 [id] FROM [dbo].[Ventas] WHERE [empresaID] = @EmpresaId AND [usuarioID] = @UsuarioId ORDER BY [fecha] DESC),
         (SELECT TOP 1 [id] FROM [dbo].[Productos] WHERE [empresaID] = @EmpresaId AND [sku] = 'K-205'),
         1, 62000.00, 29000.00, 113.79, 33000.00);
END;
GO

-- ============================================================
-- CUENTA Y DATOS DE DEMOSTRACION PARA LA EXPOSICION
-- ============================================================
-- Esta seccion es idempotente. Al volver a ejecutar el script, conserva una
-- sola cuenta demo y actualiza sus fechas para mantener ventas de hoy,
-- dias, semanas y meses anteriores.

DECLARE @DemoEmpresaId int;
DECLARE @DemoPermisosId int;
DECLARE @DemoRolId int;
DECLARE @DemoUsuarioId int;

IF NOT EXISTS (SELECT 1 FROM [dbo].[Empresa] WHERE [nombre] = 'Almacén Horizonte Demo')
BEGIN
    INSERT INTO [dbo].[Empresa] ([nombre]) VALUES ('Almacén Horizonte Demo');
END;
SET @DemoEmpresaId = (SELECT TOP 1 [id] FROM [dbo].[Empresa] WHERE [nombre] = 'Almacén Horizonte Demo' ORDER BY [id]);
SET @DemoPermisosId = (
    SELECT TOP 1 [id]
    FROM [dbo].[Permisos]
    WHERE [registrarVentas] = 1
      AND [verHistorialVentas] = 1
      AND [anularVentas] = 1
      AND [verProductos] = 1
      AND [crearProductos] = 1
      AND [editarProductos] = 1
      AND [eliminarProductos] = 1
      AND [verStock] = 1
      AND [verDashboard] = 1
      AND [gestionarUsuarios] = 1
      AND [verGanacia] = 1
    ORDER BY [id]
);

IF @DemoPermisosId IS NULL
BEGIN
    INSERT INTO [dbo].[Permisos]
        ([registrarVentas], [verHistorialVentas], [anularVentas], [verProductos], [crearProductos], [editarProductos], [eliminarProductos], [verStock], [verDashboard], [gestionarUsuarios], [verGanacia])
    VALUES
        (1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1);

    SET @DemoPermisosId = CAST(SCOPE_IDENTITY() AS int);
END;

IF NOT EXISTS (
    SELECT 1
    FROM [dbo].[Rol]
    WHERE [empresaID] = @DemoEmpresaId
      AND [nombre] = 'Administrador Expo'
)
BEGIN
    INSERT INTO [dbo].[Rol] ([empresaID], [nombre], [permisoID])
    VALUES (@DemoEmpresaId, 'Administrador Expo', @DemoPermisosId);
END;
SET @DemoRolId = (
    SELECT TOP 1 [id]
    FROM [dbo].[Rol]
    WHERE [empresaID] = @DemoEmpresaId
      AND [nombre] = 'Administrador Expo'
    ORDER BY [id]
);

UPDATE [dbo].[Rol]
SET [permisoID] = @DemoPermisosId
WHERE [id] = @DemoRolId;

IF NOT EXISTS (SELECT 1 FROM [dbo].[Usuarios] WHERE [mail] = 'demo@quantitary.com')
BEGIN
    INSERT INTO [dbo].[Usuarios]
        ([nombreUsuario], [contrasena], [mail], [numeroTelefono], [empresaID], [rolID])
    VALUES
        ('Usuario Demo', 'Expo2026', 'demo@quantitary.com', NULL, @DemoEmpresaId, @DemoRolId);
END;
SET @DemoUsuarioId = (
    SELECT TOP 1 [id]
    FROM [dbo].[Usuarios]
    WHERE [mail] = 'demo@quantitary.com'
    ORDER BY [id]
);

-- Mantener la cuenta demo asociada a su empresa y rol aunque la base ya
-- tuviera una version anterior de estos datos.
UPDATE [dbo].[Usuarios]
SET [nombreUsuario] = 'Usuario Demo',
    [contrasena] = 'Expo2026',
    [empresaID] = @DemoEmpresaId,
    [rolID] = @DemoRolId
WHERE [id] = @DemoUsuarioId;

DECLARE @CategoriasDemo TABLE ([nombre] varchar(100) NOT NULL);
INSERT INTO @CategoriasDemo ([nombre])
VALUES ('Bebidas'), ('Almacén'), ('Lácteos'), ('Snacks'), ('Limpieza');

INSERT INTO [dbo].[Categoria] ([empresaID], [nombre])
SELECT @DemoEmpresaId, d.[nombre]
FROM @CategoriasDemo d
WHERE NOT EXISTS (
    SELECT 1
    FROM [dbo].[Categoria] c
    WHERE c.[empresaID] = @DemoEmpresaId
      AND c.[nombre] = d.[nombre]
);

DECLARE @ProveedoresDemo TABLE ([nombre] varchar(150) NOT NULL);
INSERT INTO @ProveedoresDemo ([nombre])
VALUES ('Distribuidora Río'), ('Mayorista Centro'), ('Lácteos del Sur'), ('Hogar Plus');

INSERT INTO [dbo].[Proveedores]
    ([empresaID], [nombre], [telefono], [mail], [costoEnvio], [detalles])
SELECT
    @DemoEmpresaId,
    d.[nombre],
    NULL,
    NULL,
    0,
    'Proveedor del catalogo de demostracion'
FROM @ProveedoresDemo d
WHERE NOT EXISTS (
    SELECT 1
    FROM [dbo].[Proveedores] p
    WHERE p.[empresaID] = @DemoEmpresaId
      AND p.[nombre] = d.[nombre]
);

DECLARE @CatalogoDemo TABLE (
    [nombre] varchar(150) NOT NULL,
    [sku] varchar(100) NOT NULL,
    [categoria] varchar(100) NOT NULL,
    [costoCompra] decimal(12,2) NOT NULL,
    [precioVenta] decimal(12,2) NOT NULL,
    [proveedor] varchar(150) NOT NULL,
    [stockActual] int NOT NULL,
    [stockMinimo] int NOT NULL
);

INSERT INTO @CatalogoDemo
    ([nombre], [sku], [categoria], [costoCompra], [precioVenta], [proveedor], [stockActual], [stockMinimo])
VALUES
    ('Coca-Cola Original 2,25 L', 'BEB-COCA-225', 'Bebidas', 2100, 3200, 'Distribuidora Río', 0, 12),
    ('Coca-Cola Original 2 L', 'BEB-COCA-200', 'Bebidas', 1950, 2900, 'Distribuidora Río', 28, 10),
    ('Agua Mineral sin Gas 1,5 L', 'BEB-AGUA-150', 'Bebidas', 550, 1000, 'Distribuidora Río', 48, 15),
    ('Jugo de Naranja 1 L', 'BEB-JUGO-100', 'Bebidas', 1200, 1900, 'Distribuidora Río', 8, 10),
    ('Yerba Mate Tradicional 1 kg', 'ALM-YERBA-100', 'Almacén', 3500, 5100, 'Mayorista Centro', 10, 10),
    ('Arroz Largo Fino 1 kg', 'ALM-ARROZ-100', 'Almacén', 1000, 1650, 'Mayorista Centro', 0, 8),
    ('Fideos Tirabuzón 500 g', 'ALM-FIDEO-050', 'Almacén', 700, 1200, 'Mayorista Centro', 7, 10),
    ('Aceite de Girasol 1,5 L', 'ALM-ACEITE-150', 'Almacén', 2300, 3450, 'Mayorista Centro', 5, 8),
    ('Azúcar 1 kg', 'ALM-AZUCAR-100', 'Almacén', 850, 1350, 'Mayorista Centro', 24, 8),
    ('Café Instantáneo 170 g', 'ALM-CAFE-017', 'Almacén', 4100, 5900, 'Mayorista Centro', 11, 5),
    ('Leche Entera 1 L', 'LAC-LECHE-100', 'Lácteos', 950, 1450, 'Lácteos del Sur', 3, 12),
    ('Yogur Natural 190 g', 'LAC-YOGUR-019', 'Lácteos', 550, 900, 'Lácteos del Sur', 25, 8),
    ('Galletitas con Chocolate 300 g', 'SNK-GALLE-030', 'Snacks', 1050, 1750, 'Mayorista Centro', 32, 10),
    ('Papas Fritas Clásicas 150 g', 'SNK-PAPAS-015', 'Snacks', 1350, 2150, 'Mayorista Centro', 14, 6),
    ('Chocolate con Leche 100 g', 'SNK-CHOCO-010', 'Snacks', 1150, 1900, 'Mayorista Centro', 4, 6),
    ('Detergente Limón 750 ml', 'LIM-DETER-075', 'Limpieza', 1250, 2050, 'Hogar Plus', 6, 8),
    ('Lavandina Concentrada 2 L', 'LIM-LAVAN-200', 'Limpieza', 850, 1400, 'Hogar Plus', 18, 7),
    ('Papel Higiénico Doble Hoja 4 rollos', 'LIM-PAPEL-004', 'Limpieza', 1900, 3050, 'Hogar Plus', 96, 15);

INSERT INTO [dbo].[Productos]
    ([empresaID], [categoriaID], [proveedorID], [nombre], [sku], [descripcion], [fechaCarga], [precioVenta], [costoCompra], [activo])
SELECT
    @DemoEmpresaId,
    c.[id],
    pr.[id],
    d.[nombre],
    d.[sku],
    'Producto del catalogo Excel de demostracion',
    GETDATE(),
    d.[precioVenta],
    d.[costoCompra],
    1
FROM @CatalogoDemo d
INNER JOIN [dbo].[Categoria] c
    ON c.[empresaID] = @DemoEmpresaId AND c.[nombre] = d.[categoria]
INNER JOIN [dbo].[Proveedores] pr
    ON pr.[empresaID] = @DemoEmpresaId AND pr.[nombre] = d.[proveedor]
WHERE NOT EXISTS (
    SELECT 1
    FROM [dbo].[Productos] p
    WHERE p.[empresaID] = @DemoEmpresaId
      AND p.[sku] = d.[sku]
);

-- Sincronizar el catalogo demo con el Excel incluido en el proyecto.
UPDATE p
SET p.[nombre] = d.[nombre],
    p.[categoriaID] = c.[id],
    p.[proveedorID] = pr.[id],
    p.[precioVenta] = d.[precioVenta],
    p.[costoCompra] = d.[costoCompra],
    p.[activo] = 1
FROM [dbo].[Productos] p
INNER JOIN @CatalogoDemo d ON d.[sku] = p.[sku]
INNER JOIN [dbo].[Categoria] c
    ON c.[empresaID] = @DemoEmpresaId AND c.[nombre] = d.[categoria]
INNER JOIN [dbo].[Proveedores] pr
    ON pr.[empresaID] = @DemoEmpresaId AND pr.[nombre] = d.[proveedor]
WHERE p.[empresaID] = @DemoEmpresaId;

INSERT INTO [dbo].[Stock]
    ([productoID], [cantidad], [cantidadMinimoAlerta], [activo], [actualizadoEn])
SELECT p.[id], d.[stockActual], d.[stockMinimo], 1, GETDATE()
FROM @CatalogoDemo d
INNER JOIN [dbo].[Productos] p
    ON p.[empresaID] = @DemoEmpresaId AND p.[sku] = d.[sku]
WHERE NOT EXISTS (
    SELECT 1 FROM [dbo].[Stock] s WHERE s.[productoID] = p.[id]
);

-- Restablecer los escenarios de stock al volver a ejecutar el script:
-- sin stock, bajo minimo, justo en el minimo y sobrestock.
UPDATE s
SET s.[cantidad] = d.[stockActual],
    s.[cantidadMinimoAlerta] = d.[stockMinimo],
    s.[activo] = 1,
    s.[actualizadoEn] = GETDATE()
FROM [dbo].[Stock] s
INNER JOIN [dbo].[Productos] p ON p.[id] = s.[productoID]
INNER JOIN @CatalogoDemo d ON d.[sku] = p.[sku]
WHERE p.[empresaID] = @DemoEmpresaId;

DECLARE @VentasDemo TABLE (
    [codigo] varchar(40) NOT NULL,
    [fecha] datetime2(0) NOT NULL,
    [sku] varchar(100) NOT NULL,
    [cantidad] int NOT NULL,
    [metodoPago] varchar(50) NOT NULL
);

INSERT INTO @VentasDemo ([codigo], [fecha], [sku], [cantidad], [metodoPago])
VALUES
    ('EXPO-DEMO-001', DATEADD(minute, -15, GETDATE()), 'BEB-COCA-200', 2, 'Tarjeta'),
    ('EXPO-DEMO-002', DATEADD(minute, -35, GETDATE()), 'BEB-AGUA-150', 3, 'Efectivo'),
    ('EXPO-DEMO-003', DATEADD(minute, -55, GETDATE()), 'ALM-YERBA-100', 1, 'Transferencia'),
    ('EXPO-DEMO-004', DATEADD(minute, -75, GETDATE()), 'SNK-GALLE-030', 2, 'Tarjeta'),
    ('EXPO-DEMO-005', DATEADD(minute, -95, GETDATE()), 'LAC-LECHE-100', 4, 'Efectivo'),
    ('EXPO-DEMO-006', DATEADD(minute, -115, GETDATE()), 'LIM-DETER-075', 1, 'Tarjeta'),
    ('EXPO-DEMO-007', DATEADD(day, -1, GETDATE()), 'BEB-COCA-225', 3, 'Efectivo'),
    ('EXPO-DEMO-008', DATEADD(day, -1, DATEADD(hour, -2, GETDATE())), 'ALM-ARROZ-100', 5, 'Transferencia'),
    ('EXPO-DEMO-009', DATEADD(day, -1, DATEADD(hour, -4, GETDATE())), 'SNK-PAPAS-015', 2, 'Tarjeta'),
    ('EXPO-DEMO-010', DATEADD(day, -2, GETDATE()), 'LAC-YOGUR-019', 4, 'Efectivo'),
    ('EXPO-DEMO-011', DATEADD(day, -3, GETDATE()), 'BEB-COCA-200', 4, 'Tarjeta'),
    ('EXPO-DEMO-012', DATEADD(day, -3, DATEADD(hour, -3, GETDATE())), 'BEB-JUGO-100', 2, 'Transferencia'),
    ('EXPO-DEMO-013', DATEADD(day, -4, GETDATE()), 'ALM-AZUCAR-100', 2, 'Efectivo'),
    ('EXPO-DEMO-014', DATEADD(day, -7, GETDATE()), 'ALM-ACEITE-150', 2, 'Tarjeta'),
    ('EXPO-DEMO-015', DATEADD(day, -7, DATEADD(hour, -2, GETDATE())), 'ALM-CAFE-017', 1, 'Transferencia'),
    ('EXPO-DEMO-016', DATEADD(day, -10, GETDATE()), 'SNK-CHOCO-010', 3, 'Efectivo'),
    ('EXPO-DEMO-017', DATEADD(day, -14, GETDATE()), 'BEB-COCA-225', 5, 'Tarjeta'),
    ('EXPO-DEMO-018', DATEADD(day, -14, DATEADD(hour, -3, GETDATE())), 'LAC-LECHE-100', 6, 'Efectivo'),
    ('EXPO-DEMO-019', DATEADD(day, -18, GETDATE()), 'SNK-GALLE-030', 3, 'Transferencia'),
    ('EXPO-DEMO-020', DATEADD(day, -21, GETDATE()), 'LIM-PAPEL-004', 1, 'Tarjeta'),
    ('EXPO-DEMO-021', DATEADD(day, -25, GETDATE()), 'BEB-AGUA-150', 4, 'Efectivo'),
    ('EXPO-DEMO-022', DATEADD(month, -1, GETDATE()), 'BEB-COCA-200', 5, 'Tarjeta'),
    ('EXPO-DEMO-023', DATEADD(month, -1, DATEADD(day, -3, GETDATE())), 'ALM-YERBA-100', 2, 'Transferencia'),
    ('EXPO-DEMO-024', DATEADD(month, -1, DATEADD(day, -8, GETDATE())), 'ALM-ARROZ-100', 4, 'Efectivo'),
    ('EXPO-DEMO-025', DATEADD(month, -1, DATEADD(day, -13, GETDATE())), 'LIM-DETER-075', 2, 'Tarjeta'),
    ('EXPO-DEMO-026', DATEADD(month, -2, GETDATE()), 'BEB-COCA-225', 6, 'Efectivo'),
    ('EXPO-DEMO-027', DATEADD(month, -2, DATEADD(day, -5, GETDATE())), 'LAC-LECHE-100', 8, 'Tarjeta'),
    ('EXPO-DEMO-028', DATEADD(month, -2, DATEADD(day, -11, GETDATE())), 'SNK-GALLE-030', 5, 'Transferencia'),
    ('EXPO-DEMO-029', DATEADD(month, -2, DATEADD(day, -17, GETDATE())), 'ALM-ACEITE-150', 3, 'Efectivo'),
    ('EXPO-DEMO-030', DATEADD(month, -3, GETDATE()), 'BEB-COCA-200', 7, 'Tarjeta'),
    ('EXPO-DEMO-031', DATEADD(month, -3, DATEADD(day, -8, GETDATE())), 'BEB-AGUA-150', 6, 'Efectivo'),
    ('EXPO-DEMO-032', DATEADD(month, -3, DATEADD(day, -16, GETDATE())), 'ALM-FIDEO-050', 4, 'Transferencia'),
    ('EXPO-DEMO-033', DATEADD(month, -5, GETDATE()), 'BEB-JUGO-100', 5, 'Tarjeta'),
    ('EXPO-DEMO-034', DATEADD(month, -5, DATEADD(day, -9, GETDATE())), 'ALM-CAFE-017', 2, 'Efectivo'),
    ('EXPO-DEMO-035', DATEADD(month, -6, GETDATE()), 'LIM-PAPEL-004', 1, 'Transferencia'),
    ('EXPO-DEMO-036', DATEADD(month, -6, DATEADD(day, -12, GETDATE())), 'SNK-CHOCO-010', 4, 'Tarjeta');

INSERT INTO [dbo].[Ventas]
    ([empresaID], [usuarioID], [fecha], [metodoPago], [totalVenta], [observacion])
SELECT
    @DemoEmpresaId,
    @DemoUsuarioId,
    d.[fecha],
    d.[metodoPago],
    p.[precioVenta] * d.[cantidad],
    d.[codigo]
FROM @VentasDemo d
INNER JOIN [dbo].[Productos] p
    ON p.[empresaID] = @DemoEmpresaId AND p.[sku] = d.[sku]
WHERE NOT EXISTS (
    SELECT 1
    FROM [dbo].[Ventas] v
    WHERE v.[empresaID] = @DemoEmpresaId
      AND v.[observacion] = d.[codigo]
);

-- Actualizar las fechas relativas permite ejecutar el script el mismo dia de
-- la exposicion y conservar siempre ventas de hoy y periodos anteriores.
UPDATE v
SET v.[usuarioID] = @DemoUsuarioId,
    v.[fecha] = d.[fecha],
    v.[metodoPago] = d.[metodoPago],
    v.[totalVenta] = p.[precioVenta] * d.[cantidad]
FROM [dbo].[Ventas] v
INNER JOIN @VentasDemo d ON d.[codigo] = v.[observacion]
INNER JOIN [dbo].[Productos] p
    ON p.[empresaID] = @DemoEmpresaId AND p.[sku] = d.[sku]
WHERE v.[empresaID] = @DemoEmpresaId;

INSERT INTO [dbo].[DetalleVenta]
    ([ventaID], [productoID], [cantidad], [precioVenta], [costoCompra], [porcentajeGanancia], [ganancia])
SELECT
    v.[id],
    p.[id],
    d.[cantidad],
    p.[precioVenta] * d.[cantidad],
    p.[costoCompra] * d.[cantidad],
    CASE
        WHEN p.[costoCompra] = 0 THEN NULL
        ELSE ROUND(((p.[precioVenta] - p.[costoCompra]) / p.[costoCompra]) * 100, 2)
    END,
    (p.[precioVenta] - p.[costoCompra]) * d.[cantidad]
FROM @VentasDemo d
INNER JOIN [dbo].[Ventas] v
    ON v.[empresaID] = @DemoEmpresaId AND v.[observacion] = d.[codigo]
INNER JOIN [dbo].[Productos] p
    ON p.[empresaID] = @DemoEmpresaId AND p.[sku] = d.[sku]
WHERE NOT EXISTS (
    SELECT 1
    FROM [dbo].[DetalleVenta] dv
    WHERE dv.[ventaID] = v.[id]
      AND dv.[productoID] = p.[id]
);

UPDATE dv
SET dv.[cantidad] = d.[cantidad],
    dv.[precioVenta] = p.[precioVenta] * d.[cantidad],
    dv.[costoCompra] = p.[costoCompra] * d.[cantidad],
    dv.[porcentajeGanancia] = CASE
        WHEN p.[costoCompra] = 0 THEN NULL
        ELSE ROUND(((p.[precioVenta] - p.[costoCompra]) / p.[costoCompra]) * 100, 2)
    END,
    dv.[ganancia] = (p.[precioVenta] - p.[costoCompra]) * d.[cantidad]
FROM [dbo].[DetalleVenta] dv
INNER JOIN [dbo].[Ventas] v ON v.[id] = dv.[ventaID]
INNER JOIN @VentasDemo d ON d.[codigo] = v.[observacion]
INNER JOIN [dbo].[Productos] p
    ON p.[empresaID] = @DemoEmpresaId AND p.[sku] = d.[sku]
WHERE v.[empresaID] = @DemoEmpresaId;
GO

-- ============================================================
-- ACTUALIZACIÓN DE STORED PROCEDURES
-- ============================================================

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_ObtenerProductosStock]
    @EmpresaId INT = NULL,
    @Sku NVARCHAR(100) = NULL,
    @Categoria NVARCHAR(100) = NULL,
    @PocoStock BIT = 0,
    @PrecioMinimo DECIMAL(12,2) = NULL,
    @Orden NVARCHAR(30) = 'nombre_asc'
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @SkuFiltro NVARCHAR(100) = NULLIF(LTRIM(RTRIM(@Sku)), '');
    DECLARE @CategoriaFiltro NVARCHAR(100) = NULLIF(LTRIM(RTRIM(@Categoria)), '');
    DECLARE @PrecioFiltro DECIMAL(12,2) = CASE WHEN @PrecioMinimo IS NOT NULL AND @PrecioMinimo > 0 THEN @PrecioMinimo ELSE NULL END;
    DECLARE @OrdenFiltro NVARCHAR(30) = CASE WHEN LTRIM(RTRIM(ISNULL(@Orden, ''))) = '' THEN 'nombre_asc' ELSE @Orden END;

    SELECT
        p.id,
        p.empresaID,
        p.nombre,
        p.sku,
        c.nombre AS categoria,
        p.descripcion,
        p.fechaCarga,
        p.precioVenta AS precio,
        ISNULL(s.cantidad, 0) AS stockActual,
        ISNULL(s.cantidadMinimoAlerta, 0) AS stockMinimo,
        pr.nombre AS proveedor,
        p.costoCompra,
        p.precioVenta,
        0 AS cantidadVentas
    FROM Productos p
    INNER JOIN Categoria c ON c.id = p.categoriaID
    LEFT JOIN Stock s ON s.productoID = p.id AND s.activo = 1
    LEFT JOIN Proveedores pr ON pr.id = p.proveedorID
    WHERE (@EmpresaId IS NULL OR p.empresaID = @EmpresaId)
      AND (@SkuFiltro IS NULL OR LOWER(LTRIM(RTRIM(ISNULL(p.sku, '')))) LIKE '%' + LOWER(LTRIM(RTRIM(@SkuFiltro))) + '%')
      AND (@CategoriaFiltro IS NULL OR LOWER(LTRIM(RTRIM(ISNULL(c.nombre, '')))) = LOWER(LTRIM(RTRIM(@CategoriaFiltro))))
      AND (@PrecioFiltro IS NULL OR p.precioVenta >= @PrecioFiltro)
      AND (@PocoStock = 0 OR (ISNULL(s.cantidad, 0) <= ISNULL(s.cantidadMinimoAlerta, 0) OR ISNULL(s.cantidad, 0) = 0))
    ORDER BY
        CASE WHEN @OrdenFiltro = 'precio_desc' THEN p.precioVenta END DESC,
        CASE WHEN @OrdenFiltro = 'precio_asc' THEN p.precioVenta END ASC,
        CASE WHEN @OrdenFiltro = 'nombre_asc' THEN p.nombre END ASC,
        CASE WHEN @OrdenFiltro = 'nombre_desc' THEN p.nombre END DESC,
        CASE WHEN @OrdenFiltro = 'fecha_nuevo' THEN p.fechaCarga END DESC,
        CASE WHEN @OrdenFiltro = 'fecha_antiguo' THEN p.fechaCarga END ASC,
        CASE WHEN @OrdenFiltro NOT IN ('precio_desc', 'precio_asc', 'nombre_asc', 'nombre_desc', 'fecha_nuevo', 'fecha_antiguo') THEN p.nombre END ASC;
END;
GO

-- ============================================================
-- RESUMEN DE FUENTE DE VERDAD
-- ============================================================
-- Productos.precioVenta       -> precio vigente actual del producto.
-- Stock.cantidad              -> inventario actual real.
-- Stock.cantidadMinimoAlerta  -> umbral de reposición.
-- DetalleVenta.precioVenta    -> snapshot histórico del precio de venta en la transacción.
-- DetalleVenta.costoCompra    -> snapshot histórico del costo al momento de vender.
-- Ventas.totalVenta           -> total del comprobante, no replica datos por producto.
-- ============================================================
