namespace Quantitary.Models
{
    public class Categoria
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;

        public List<Productos> Productos { get; set; } = new List<Productos>();
    }
}