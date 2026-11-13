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

-- Compatibilidad con la tabla Productos anterior. Esa versión guardaba
-- categoría, proveedor, precio y stock directamente en Productos y varias
-- de esas columnas eran NOT NULL. El esquema actual conserva esos datos en
-- Categoria, Proveedores y Stock, por lo que las columnas antiguas deben
-- aceptar NULL para no bloquear altas nuevas.
DECLARE @ColumnaProductoLegacy sysname;
DECLARE @TipoProductoLegacy nvarchar(128);
DECLARE @SqlProductoLegacy nvarchar(max);

DECLARE cursor_productos_legacy CURSOR LOCAL FAST_FORWARD FOR
SELECT
    c.[name],
    CASE
        WHEN t.[name] IN ('varchar', 'char', 'varbinary', 'binary')
            THEN t.[name] + '(' + CASE WHEN c.[max_length] = -1 THEN 'max' ELSE CONVERT(varchar(10), c.[max_length]) END + ')'
        WHEN t.[name] IN ('nvarchar', 'nchar')
            THEN t.[name] + '(' + CASE WHEN c.[max_length] = -1 THEN 'max' ELSE CONVERT(varchar(10), c.[max_length] / 2) END + ')'
        WHEN t.[name] IN ('decimal', 'numeric')
            THEN t.[name] + '(' + CONVERT(varchar(10), c.[precision]) + ',' + CONVERT(varchar(10), c.[scale]) + ')'
        WHEN t.[name] IN ('datetime2', 'datetimeoffset', 'time')
            THEN t.[name] + '(' + CONVERT(varchar(10), c.[scale]) + ')'
        ELSE t.[name]
    END
FROM sys.columns c
INNER JOIN sys.types t ON t.[user_type_id] = c.[user_type_id]
WHERE c.[object_id] = OBJECT_ID(N'dbo.Productos')
  AND c.[name] IN (
      'nombreProducto', 'categoria', 'proveedor', 'marca', 'precio',
      'stockActual', 'stockMinimo', 'peso', 'unidadMedida', 'cantidadVentas'
  )
  AND c.[is_nullable] = 0
  AND c.[is_identity] = 0
  AND c.[is_computed] = 0;

OPEN cursor_productos_legacy;
FETCH NEXT FROM cursor_productos_legacy INTO @ColumnaProductoLegacy, @TipoProductoLegacy;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @SqlProductoLegacy =
        N'ALTER TABLE dbo.Productos ALTER COLUMN ' + QUOTENAME(@ColumnaProductoLegacy) +
        N' ' + @TipoProductoLegacy + N' NULL;';
    EXEC sys.sp_executesql @SqlProductoLegacy;

    FETCH NEXT FROM cursor_productos_legacy INTO @ColumnaProductoLegacy, @TipoProductoLegacy;
END;

CLOSE cursor_productos_legacy;
DEALLOCATE cursor_productos_legacy;
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

-- ============================================================
-- COMPATIBILIDAD CON BASES ANTERIORES
-- ============================================================
-- Las tablas pueden existir con el modelo viejo. Se agregan únicamente
-- las columnas faltantes y se conservan los registros actuales.

IF COL_LENGTH(N'dbo.Categoria', N'empresaID') IS NULL
    ALTER TABLE dbo.Categoria ADD empresaID int NULL;
IF COL_LENGTH(N'dbo.Categoria', N'nombre') IS NULL
    ALTER TABLE dbo.Categoria ADD nombre varchar(100) NULL;

IF COL_LENGTH(N'dbo.Proveedores', N'empresaID') IS NULL
    ALTER TABLE dbo.Proveedores ADD empresaID int NULL;
IF COL_LENGTH(N'dbo.Proveedores', N'nombre') IS NULL
    ALTER TABLE dbo.Proveedores ADD nombre varchar(150) NULL;
IF COL_LENGTH(N'dbo.Proveedores', N'telefono') IS NULL
    ALTER TABLE dbo.Proveedores ADD telefono varchar(30) NULL;
IF COL_LENGTH(N'dbo.Proveedores', N'mail') IS NULL
    ALTER TABLE dbo.Proveedores ADD mail varchar(150) NULL;
IF COL_LENGTH(N'dbo.Proveedores', N'costoEnvio') IS NULL
    ALTER TABLE dbo.Proveedores ADD costoEnvio decimal(12,2) NULL;
IF COL_LENGTH(N'dbo.Proveedores', N'detalles') IS NULL
    ALTER TABLE dbo.Proveedores ADD detalles varchar(max) NULL;

IF COL_LENGTH(N'dbo.Productos', N'empresaID') IS NULL
    ALTER TABLE dbo.Productos ADD empresaID int NULL;
IF COL_LENGTH(N'dbo.Productos', N'categoriaID') IS NULL
    ALTER TABLE dbo.Productos ADD categoriaID int NULL;
IF COL_LENGTH(N'dbo.Productos', N'proveedorID') IS NULL
    ALTER TABLE dbo.Productos ADD proveedorID int NULL;
IF COL_LENGTH(N'dbo.Productos', N'nombre') IS NULL
    ALTER TABLE dbo.Productos ADD nombre varchar(150) NULL;
IF COL_LENGTH(N'dbo.Productos', N'sku') IS NULL
    ALTER TABLE dbo.Productos ADD sku varchar(100) NULL;
IF COL_LENGTH(N'dbo.Productos', N'descripcion') IS NULL
    ALTER TABLE dbo.Productos ADD descripcion varchar(500) NULL;
IF COL_LENGTH(N'dbo.Productos', N'fechaCarga') IS NULL
    ALTER TABLE dbo.Productos ADD fechaCarga datetime2(0) NULL;
IF COL_LENGTH(N'dbo.Productos', N'precioVenta') IS NULL
    ALTER TABLE dbo.Productos ADD precioVenta decimal(12,2) NULL;
IF COL_LENGTH(N'dbo.Productos', N'costoCompra') IS NULL
    ALTER TABLE dbo.Productos ADD costoCompra decimal(12,2) NULL;
IF COL_LENGTH(N'dbo.Productos', N'activo') IS NULL
    ALTER TABLE dbo.Productos ADD activo bit NULL;

IF COL_LENGTH(N'dbo.Stock', N'productoID') IS NULL
    ALTER TABLE dbo.Stock ADD productoID int NULL;
IF COL_LENGTH(N'dbo.Stock', N'cantidad') IS NULL
    ALTER TABLE dbo.Stock ADD cantidad int NULL;
IF COL_LENGTH(N'dbo.Stock', N'cantidadMinimoAlerta') IS NULL
    ALTER TABLE dbo.Stock ADD cantidadMinimoAlerta int NULL;
IF COL_LENGTH(N'dbo.Stock', N'activo') IS NULL
    ALTER TABLE dbo.Stock ADD activo bit NULL;
IF COL_LENGTH(N'dbo.Stock', N'actualizadoEn') IS NULL
    ALTER TABLE dbo.Stock ADD actualizadoEn datetime2(0) NULL;

IF COL_LENGTH(N'dbo.Ventas', N'empresaID') IS NULL
    ALTER TABLE dbo.Ventas ADD empresaID int NULL;
IF COL_LENGTH(N'dbo.Ventas', N'usuarioID') IS NULL
    ALTER TABLE dbo.Ventas ADD usuarioID int NULL;
IF COL_LENGTH(N'dbo.Ventas', N'fecha') IS NULL
    ALTER TABLE dbo.Ventas ADD fecha datetime2(0) NULL;
IF COL_LENGTH(N'dbo.Ventas', N'metodoPago') IS NULL
    ALTER TABLE dbo.Ventas ADD metodoPago varchar(50) NULL;
IF COL_LENGTH(N'dbo.Ventas', N'totalVenta') IS NULL
    ALTER TABLE dbo.Ventas ADD totalVenta decimal(12,2) NULL;
IF COL_LENGTH(N'dbo.Ventas', N'observacion') IS NULL
    ALTER TABLE dbo.Ventas ADD observacion varchar(500) NULL;

IF COL_LENGTH(N'dbo.DetalleVenta', N'ventaID') IS NULL
    ALTER TABLE dbo.DetalleVenta ADD ventaID int NULL;
IF COL_LENGTH(N'dbo.DetalleVenta', N'productoID') IS NULL
    ALTER TABLE dbo.DetalleVenta ADD productoID int NULL;
IF COL_LENGTH(N'dbo.DetalleVenta', N'cantidad') IS NULL
    ALTER TABLE dbo.DetalleVenta ADD cantidad int NULL;
IF COL_LENGTH(N'dbo.DetalleVenta', N'precioVenta') IS NULL
    ALTER TABLE dbo.DetalleVenta ADD precioVenta decimal(12,2) NULL;
IF COL_LENGTH(N'dbo.DetalleVenta', N'costoCompra') IS NULL
    ALTER TABLE dbo.DetalleVenta ADD costoCompra decimal(12,2) NULL;
IF COL_LENGTH(N'dbo.DetalleVenta', N'porcentajeGanancia') IS NULL
    ALTER TABLE dbo.DetalleVenta ADD porcentajeGanancia decimal(10,2) NULL;
IF COL_LENGTH(N'dbo.DetalleVenta', N'ganancia') IS NULL
    ALTER TABLE dbo.DetalleVenta ADD ganancia decimal(12,2) NULL;
GO

-- Proveedores antiguos exigían productoPrincipal en cada alta. Se conserva
-- la columna, pero pasa a ser opcional antes de migrar nombres de proveedor.
IF COL_LENGTH(N'dbo.Proveedores', N'productoPrincipal') IS NOT NULL
BEGIN
    DECLARE @TipoProductoPrincipal nvarchar(128);
    SELECT @TipoProductoPrincipal =
        CASE
            WHEN t.name IN ('varchar', 'char')
                THEN t.name + '(' + CASE WHEN c.max_length = -1 THEN 'max' ELSE CONVERT(varchar(10), c.max_length) END + ')'
            WHEN t.name IN ('nvarchar', 'nchar')
                THEN t.name + '(' + CASE WHEN c.max_length = -1 THEN 'max' ELSE CONVERT(varchar(10), c.max_length / 2) END + ')'
            ELSE t.name
        END
    FROM sys.columns c
    INNER JOIN sys.types t ON t.user_type_id = c.user_type_id
    WHERE c.object_id = OBJECT_ID(N'dbo.Proveedores')
      AND c.name = 'productoPrincipal'
      AND c.is_nullable = 0;

    IF @TipoProductoPrincipal IS NOT NULL
        EXEC (N'ALTER TABLE dbo.Proveedores ALTER COLUMN productoPrincipal '
            + @TipoProductoPrincipal + N' NULL;');
END;
GO

DECLARE @EmpresaCompatId int = (SELECT TOP 1 id FROM dbo.Empresa ORDER BY id);
IF @EmpresaCompatId IS NULL
BEGIN
    INSERT INTO dbo.Empresa (nombre) VALUES ('Quantitary Admin');
    SET @EmpresaCompatId = CAST(SCOPE_IDENTITY() AS int);
END;

UPDATE dbo.Categoria
SET empresaID = @EmpresaCompatId
WHERE empresaID IS NULL OR empresaID NOT IN (SELECT id FROM dbo.Empresa);

UPDATE dbo.Categoria
SET nombre = 'Categoría ' + CAST(id AS varchar(20))
WHERE nombre IS NULL OR LTRIM(RTRIM(nombre)) = '';

UPDATE dbo.Proveedores
SET empresaID = @EmpresaCompatId
WHERE empresaID IS NULL OR empresaID NOT IN (SELECT id FROM dbo.Empresa);

IF COL_LENGTH(N'dbo.Proveedores', N'productoPrincipal') IS NOT NULL
BEGIN
    EXEC sys.sp_executesql N'
        UPDATE dbo.Proveedores
        SET nombre = COALESCE(NULLIF(LTRIM(RTRIM(nombre)), ''''),
                              NULLIF(LTRIM(RTRIM(productoPrincipal)), ''''),
                              ''Proveedor '' + CAST(id AS varchar(20)))
        WHERE nombre IS NULL OR LTRIM(RTRIM(nombre)) = '''';';
END;

UPDATE dbo.Proveedores
SET nombre = 'Proveedor ' + CAST(id AS varchar(20))
WHERE nombre IS NULL OR LTRIM(RTRIM(nombre)) = '';

UPDATE dbo.Proveedores SET costoEnvio = 0 WHERE costoEnvio IS NULL;

UPDATE dbo.Productos
SET empresaID = @EmpresaCompatId
WHERE empresaID IS NULL OR empresaID NOT IN (SELECT id FROM dbo.Empresa);

IF COL_LENGTH(N'dbo.Productos', N'nombreProducto') IS NOT NULL
BEGIN
    EXEC sys.sp_executesql N'
        UPDATE dbo.Productos
        SET nombre = COALESCE(NULLIF(LTRIM(RTRIM(nombre)), ''''),
                              NULLIF(LTRIM(RTRIM(nombreProducto)), ''''))
        WHERE nombre IS NULL OR LTRIM(RTRIM(nombre)) = '''';';
END;

UPDATE dbo.Productos
SET nombre = 'Producto ' + CAST(id AS varchar(20))
WHERE nombre IS NULL OR LTRIM(RTRIM(nombre)) = '';

UPDATE dbo.Productos
SET sku = 'LEGACY-' + CAST(id AS varchar(20))
WHERE sku IS NULL OR LTRIM(RTRIM(sku)) = '';

IF COL_LENGTH(N'dbo.Productos', N'precio') IS NOT NULL
BEGIN
    EXEC sys.sp_executesql N'
        UPDATE dbo.Productos
        SET precioVenta = ISNULL(precio, 0)
        WHERE precioVenta IS NULL OR precioVenta = 0;';
END;

IF COL_LENGTH(N'dbo.Productos', N'valorVenta') IS NOT NULL
BEGIN
    EXEC sys.sp_executesql N'
        UPDATE dbo.Productos
        SET precioVenta = ISNULL(valorVenta, 0)
        WHERE precioVenta IS NULL OR precioVenta = 0;';
END;

UPDATE dbo.Productos
SET precioVenta = ISNULL(precioVenta, 0),
    costoCompra = ISNULL(costoCompra, 0),
    fechaCarga = ISNULL(fechaCarga, SYSUTCDATETIME()),
    activo = ISNULL(activo, 1);

IF COL_LENGTH(N'dbo.Productos', N'categoria') IS NOT NULL
BEGIN
    EXEC sys.sp_executesql N'
        INSERT INTO dbo.Categoria (empresaID, nombre)
        SELECT DISTINCT p.empresaID, LTRIM(RTRIM(p.categoria))
        FROM dbo.Productos p
        WHERE p.categoria IS NOT NULL
          AND LTRIM(RTRIM(p.categoria)) <> ''''
          AND NOT EXISTS (
              SELECT 1 FROM dbo.Categoria c
              WHERE c.empresaID = p.empresaID
                AND LOWER(LTRIM(RTRIM(c.nombre))) = LOWER(LTRIM(RTRIM(p.categoria))));

        UPDATE p
        SET categoriaID = c.id
        FROM dbo.Productos p
        INNER JOIN dbo.Categoria c
            ON c.empresaID = p.empresaID
           AND LOWER(LTRIM(RTRIM(c.nombre))) = LOWER(LTRIM(RTRIM(p.categoria)))
        WHERE p.categoriaID IS NULL
           OR NOT EXISTS (SELECT 1 FROM dbo.Categoria actual WHERE actual.id = p.categoriaID);';
END;

INSERT INTO dbo.Categoria (empresaID, nombre)
SELECT DISTINCT p.empresaID, 'Sin categoría'
FROM dbo.Productos p
WHERE (p.categoriaID IS NULL OR NOT EXISTS (SELECT 1 FROM dbo.Categoria c WHERE c.id = p.categoriaID))
  AND NOT EXISTS (
      SELECT 1 FROM dbo.Categoria c
      WHERE c.empresaID = p.empresaID AND c.nombre = 'Sin categoría');

UPDATE p
SET categoriaID = c.id
FROM dbo.Productos p
INNER JOIN dbo.Categoria c ON c.empresaID = p.empresaID AND c.nombre = 'Sin categoría'
WHERE p.categoriaID IS NULL
   OR NOT EXISTS (SELECT 1 FROM dbo.Categoria actual WHERE actual.id = p.categoriaID);

IF COL_LENGTH(N'dbo.Productos', N'proveedor') IS NOT NULL
BEGIN
    EXEC sys.sp_executesql N'
        INSERT INTO dbo.Proveedores (empresaID, nombre, costoEnvio)
        SELECT DISTINCT p.empresaID, LTRIM(RTRIM(p.proveedor)), 0
        FROM dbo.Productos p
        WHERE p.proveedor IS NOT NULL
          AND LTRIM(RTRIM(p.proveedor)) <> ''''
          AND NOT EXISTS (
              SELECT 1 FROM dbo.Proveedores pr
              WHERE pr.empresaID = p.empresaID
                AND LOWER(LTRIM(RTRIM(pr.nombre))) = LOWER(LTRIM(RTRIM(p.proveedor))));

        UPDATE p
        SET proveedorID = pr.id
        FROM dbo.Productos p
        INNER JOIN dbo.Proveedores pr
            ON pr.empresaID = p.empresaID
           AND LOWER(LTRIM(RTRIM(pr.nombre))) = LOWER(LTRIM(RTRIM(p.proveedor)))
        WHERE p.proveedorID IS NULL
           OR NOT EXISTS (SELECT 1 FROM dbo.Proveedores actual WHERE actual.id = p.proveedorID);';
END;

UPDATE p
SET proveedorID = NULL
FROM dbo.Productos p
WHERE p.proveedorID IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.Proveedores pr WHERE pr.id = p.proveedorID);

UPDATE dbo.Stock
SET cantidad = ISNULL(cantidad, 0),
    cantidadMinimoAlerta = ISNULL(cantidadMinimoAlerta, 0),
    activo = ISNULL(activo, 1),
    actualizadoEn = ISNULL(actualizadoEn, SYSUTCDATETIME());

IF COL_LENGTH(N'dbo.Ventas', N'productoId') IS NOT NULL
BEGIN
    EXEC sys.sp_executesql N'
        UPDATE v
        SET empresaID = p.empresaID
        FROM dbo.Ventas v
        INNER JOIN dbo.Productos p ON p.id = v.productoId
        WHERE v.empresaID IS NULL;';
END;

UPDATE dbo.Ventas
SET empresaID = @EmpresaCompatId
WHERE empresaID IS NULL OR empresaID NOT IN (SELECT id FROM dbo.Empresa);

UPDATE v
SET usuarioID = u.id
FROM dbo.Ventas v
OUTER APPLY (
    SELECT TOP 1 id
    FROM dbo.Usuarios u
    WHERE u.empresaID = v.empresaID
    ORDER BY id
) u
WHERE v.usuarioID IS NULL;

IF COL_LENGTH(N'dbo.Ventas', N'precio') IS NOT NULL
BEGIN
    EXEC sys.sp_executesql N'
        UPDATE dbo.Ventas
        SET totalVenta = ISNULL(precio, 0)
        WHERE totalVenta IS NULL OR totalVenta = 0;';
END;

UPDATE dbo.Ventas
SET fecha = ISNULL(fecha, SYSUTCDATETIME()),
    metodoPago = COALESCE(NULLIF(LTRIM(RTRIM(metodoPago)), ''), 'Efectivo'),
    totalVenta = ISNULL(totalVenta, 0);

UPDATE dbo.DetalleVenta SET cantidad = 1 WHERE cantidad IS NULL OR cantidad <= 0;
UPDATE dbo.DetalleVenta SET precioVenta = 0 WHERE precioVenta IS NULL;

IF COL_LENGTH(N'dbo.DetalleVenta', N'precioCompra') IS NOT NULL
BEGIN
    EXEC sys.sp_executesql N'
        UPDATE dbo.DetalleVenta
        SET costoCompra = ISNULL(precioCompra, 0)
        WHERE costoCompra IS NULL OR costoCompra = 0;';
END;

UPDATE dbo.DetalleVenta
SET costoCompra = ISNULL(costoCompra, 0),
    ganancia = ISNULL(ganancia, ISNULL(precioVenta, 0) - ISNULL(costoCompra, 0));
GO

-- Las columnas obligatorias del modelo viejo quedan opcionales para que
-- las altas nuevas del esquema normalizado no fallen.
DECLARE @TablaLegacy sysname;
DECLARE @ColumnaLegacy sysname;
DECLARE @TipoLegacy nvarchar(128);
DECLARE @SqlLegacy nvarchar(max);

DECLARE cursor_legacy_nullable CURSOR LOCAL FAST_FORWARD FOR
SELECT
    OBJECT_SCHEMA_NAME(c.object_id) + N'.' + OBJECT_NAME(c.object_id),
    c.name,
    CASE
        WHEN t.name IN ('varchar', 'char', 'varbinary', 'binary')
            THEN t.name + '(' + CASE WHEN c.max_length = -1 THEN 'max' ELSE CONVERT(varchar(10), c.max_length) END + ')'
        WHEN t.name IN ('nvarchar', 'nchar')
            THEN t.name + '(' + CASE WHEN c.max_length = -1 THEN 'max' ELSE CONVERT(varchar(10), c.max_length / 2) END + ')'
        WHEN t.name IN ('decimal', 'numeric')
            THEN t.name + '(' + CONVERT(varchar(10), c.precision) + ',' + CONVERT(varchar(10), c.scale) + ')'
        WHEN t.name IN ('datetime2', 'datetimeoffset', 'time')
            THEN t.name + '(' + CONVERT(varchar(10), c.scale) + ')'
        ELSE t.name
    END
FROM sys.columns c
INNER JOIN sys.types t ON t.user_type_id = c.user_type_id
WHERE (
        (c.object_id = OBJECT_ID(N'dbo.Productos') AND c.name IN (
            'nombreProducto', 'categoria', 'proveedor', 'marca', 'precio',
            'valorVenta', 'stockActual', 'stockMinimo', 'peso', 'unidadMedida',
            'cantidadVentas', 'cantidadDeVentas', 'proveedorID'))
     OR (c.object_id = OBJECT_ID(N'dbo.Proveedores') AND c.name = 'productoPrincipal')
     OR (c.object_id = OBJECT_ID(N'dbo.Stock') AND c.name IN (
            'precioUnitario', 'rotacionMensual'))
     OR (c.object_id = OBJECT_ID(N'dbo.Ventas') AND c.name IN (
            'productoId', 'nombreProducto', 'cantidad', 'precio', 'costo'))
     OR (c.object_id = OBJECT_ID(N'dbo.DetalleVenta') AND c.name = 'precioCompra')
      )
  AND c.is_nullable = 0
  AND c.is_identity = 0
  AND c.is_computed = 0;

OPEN cursor_legacy_nullable;
FETCH NEXT FROM cursor_legacy_nullable INTO @TablaLegacy, @ColumnaLegacy, @TipoLegacy;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @SqlLegacy = N'ALTER TABLE ' + @TablaLegacy + N' ALTER COLUMN '
        + QUOTENAME(@ColumnaLegacy) + N' ' + @TipoLegacy + N' NULL;';
    EXEC sys.sp_executesql @SqlLegacy;
    FETCH NEXT FROM cursor_legacy_nullable INTO @TablaLegacy, @ColumnaLegacy, @TipoLegacy;
END;
CLOSE cursor_legacy_nullable;
DEALLOCATE cursor_legacy_nullable;
GO

-- Índice de lectura para bases anteriores. No se consolidan ni eliminan ventas.
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name IN ('UQ_DetalleVenta_Venta_Producto', 'IX_DetalleVenta_Venta_Producto')
      AND object_id = OBJECT_ID('dbo.DetalleVenta')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_DetalleVenta_Venta_Producto
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
-- CUENTA DEMO
-- ============================================================
-- La cuenta demo y su catálogo Excel se inicializan desde la aplicación
-- al ingresar con demo@quantitary.com. No requieren datos manuales aquí.

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
