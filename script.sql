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
        CONSTRAINT [CK_DetalleVenta_Costo_Positive] CHECK ([costoCompra] >= 0)
    ) ON [PRIMARY];
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

-- Empresa relacionada
ALTER TABLE [dbo].[Categoria] WITH CHECK ADD CONSTRAINT [FK_Categoria_Empresa]
    FOREIGN KEY([empresaID]) REFERENCES [dbo].[Empresa] ([id])
    ON UPDATE CASCADE ON DELETE NO ACTION;
GO
ALTER TABLE [dbo].[Categoria] CHECK CONSTRAINT [FK_Categoria_Empresa];
GO

ALTER TABLE [dbo].[Proveedores] WITH CHECK ADD CONSTRAINT [FK_Proveedores_Empresa]
    FOREIGN KEY([empresaID]) REFERENCES [dbo].[Empresa] ([id])
    ON UPDATE CASCADE ON DELETE NO ACTION;
GO
ALTER TABLE [dbo].[Proveedores] CHECK CONSTRAINT [FK_Proveedores_Empresa];
GO

ALTER TABLE [dbo].[Rol] WITH CHECK ADD CONSTRAINT [FK_Rol_Permisos]
    FOREIGN KEY([permisoID]) REFERENCES [dbo].[Permisos] ([id])
    ON UPDATE CASCADE ON DELETE NO ACTION;
GO
ALTER TABLE [dbo].[Rol] CHECK CONSTRAINT [FK_Rol_Permisos];
GO

ALTER TABLE [dbo].[Usuarios] WITH CHECK ADD CONSTRAINT [FK_Usuarios_Empresa]
    FOREIGN KEY([empresaID]) REFERENCES [dbo].[Empresa] ([id])
    ON UPDATE CASCADE ON DELETE NO ACTION;
GO
ALTER TABLE [dbo].[Usuarios] CHECK CONSTRAINT [FK_Usuarios_Empresa];
GO

ALTER TABLE [dbo].[Usuarios] WITH CHECK ADD CONSTRAINT [FK_Usuarios_Rol]
    FOREIGN KEY([rolID]) REFERENCES [dbo].[Rol] ([id])
    ON UPDATE CASCADE ON DELETE NO ACTION;
GO
ALTER TABLE [dbo].[Usuarios] CHECK CONSTRAINT [FK_Usuarios_Rol];
GO

ALTER TABLE [dbo].[Productos] WITH CHECK ADD CONSTRAINT [FK_Productos_Empresa]
    FOREIGN KEY([empresaID]) REFERENCES [dbo].[Empresa] ([id])
    ON UPDATE CASCADE ON DELETE NO ACTION;
GO
ALTER TABLE [dbo].[Productos] CHECK CONSTRAINT [FK_Productos_Empresa];
GO

ALTER TABLE [dbo].[Productos] WITH CHECK ADD CONSTRAINT [FK_Productos_Categoria]
    FOREIGN KEY([categoriaID]) REFERENCES [dbo].[Categoria] ([id])
    ON UPDATE CASCADE ON DELETE NO ACTION;
GO
ALTER TABLE [dbo].[Productos] CHECK CONSTRAINT [FK_Productos_Categoria];
GO

ALTER TABLE [dbo].[Productos] WITH CHECK ADD CONSTRAINT [FK_Productos_Proveedor]
    FOREIGN KEY([proveedorID]) REFERENCES [dbo].[Proveedores] ([id])
    ON UPDATE CASCADE ON DELETE SET NULL;
GO
ALTER TABLE [dbo].[Productos] CHECK CONSTRAINT [FK_Productos_Proveedor];
GO

ALTER TABLE [dbo].[Stock] WITH CHECK ADD CONSTRAINT [FK_Stock_Productos]
    FOREIGN KEY([productoID]) REFERENCES [dbo].[Productos] ([id])
    ON UPDATE CASCADE ON DELETE CASCADE;
GO
ALTER TABLE [dbo].[Stock] CHECK CONSTRAINT [FK_Stock_Productos];
GO

ALTER TABLE [dbo].[Ventas] WITH CHECK ADD CONSTRAINT [FK_Ventas_Empresa]
    FOREIGN KEY([empresaID]) REFERENCES [dbo].[Empresa] ([id])
    ON UPDATE CASCADE ON DELETE NO ACTION;
GO
ALTER TABLE [dbo].[Ventas] CHECK CONSTRAINT [FK_Ventas_Empresa];
GO

ALTER TABLE [dbo].[Ventas] WITH CHECK ADD CONSTRAINT [FK_Ventas_Usuarios]
    FOREIGN KEY([usuarioID]) REFERENCES [dbo].[Usuarios] ([id])
    ON UPDATE CASCADE ON DELETE NO ACTION;
GO
ALTER TABLE [dbo].[Ventas] CHECK CONSTRAINT [FK_Ventas_Usuarios];
GO

ALTER TABLE [dbo].[DetalleVenta] WITH CHECK ADD CONSTRAINT [FK_DetalleVenta_Ventas]
    FOREIGN KEY([ventaID]) REFERENCES [dbo].[Ventas] ([id])
    ON UPDATE CASCADE ON DELETE CASCADE;
GO
ALTER TABLE [dbo].[DetalleVenta] CHECK CONSTRAINT [FK_DetalleVenta_Ventas];
GO

ALTER TABLE [dbo].[DetalleVenta] WITH CHECK ADD CONSTRAINT [FK_DetalleVenta_Productos]
    FOREIGN KEY([productoID]) REFERENCES [dbo].[Productos] ([id])
    ON UPDATE CASCADE ON DELETE NO ACTION;
GO
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
