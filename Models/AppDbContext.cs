using Microsoft.EntityFrameworkCore;

namespace Quantitary.Models;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {}

    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Venta> Ventas => Set<Venta>();
    public DbSet<DetalleVenta> DetalleVentas => Set<DetalleVenta>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Ignore<Productos>();

        modelBuilder.Entity<Empresa>().ToTable("Empresa");
        modelBuilder.Entity<Usuario>().ToTable("Usuarios");
        modelBuilder.Entity<Producto>().ToTable("Productos");
        modelBuilder.Entity<Venta>().ToTable("Ventas");
        modelBuilder.Entity<DetalleVenta>().ToTable("DetalleVenta");

        modelBuilder.Entity<Usuario>()
            .HasOne(u => u.Empresa)
            .WithMany(e => e.Usuarios)
            .HasForeignKey(u => u.EmpresaID);

        modelBuilder.Entity<DetalleVenta>()
            .HasOne(d => d.Venta)
            .WithMany(v => v.DetalleVentas)
            .HasForeignKey(d => d.VentaID);

        base.OnModelCreating(modelBuilder);
    }
}
