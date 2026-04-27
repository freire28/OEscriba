using Microsoft.EntityFrameworkCore;
using MCOEWeb.Data.Entities;

namespace MCOEWeb.Data;

/// <summary>
/// Contexto EF Core para o banco <c>OESCRIBA</c> (SQL Server).
/// </summary>
public class OescribaDbContext : DbContext
{
    public OescribaDbContext(DbContextOptions<OescribaDbContext> options)
        : base(options)
    {
    }

    public DbSet<Pedido> Pedidos => Set<Pedido>();

    public DbSet<NotaFiscal> NotasFiscais => Set<NotaFiscal>();

    public DbSet<ItensPedido> ItensPedidos => Set<ItensPedido>();

    public DbSet<ConsolidadoVendas> ConsolidadoVendas => Set<ConsolidadoVendas>();

    public DbSet<Produto> Produtos => Set<Produto>();

    public DbSet<Sincronizacao> Sincronizacoes => Set<Sincronizacao>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Pedido>(entity =>
        {
            entity.ToTable("PEDIDOS", "dbo");

            entity.HasKey(e => new { e.IdPedido, e.IdTiny });

            entity.Property(e => e.IdPedido)
                .HasColumnName("ID_PEDIDO")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.IdTiny)
                .HasColumnName("ID_TINY")
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.NumeroEcommerce)
                .HasColumnName("numero_ecommerce")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.DataPedido)
                .HasColumnName("DATA_PEDIDO")
                .IsRequired();

            entity.Property(e => e.DataSincronizacao)
                .HasColumnName("DATA_SINCRONIZACAO")
                .IsRequired();

            entity.Property(e => e.IdNotaFiscal)
                .HasColumnName("id_nota_fiscal")
                .HasMaxLength(30);
        });

        modelBuilder.Entity<NotaFiscal>(entity =>
        {
            entity.ToTable("NOTAS_FISCAIS", "dbo");

            entity.HasKey(e => new { e.IdNotaFiscal, e.IdTiny });

            entity.Property(e => e.IdNotaFiscal)
                .HasColumnName("ID_NOTAFISCAL")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.IdTiny)
                .HasColumnName("ID_TINY")
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.XmlNota)
                .HasColumnName("XML_NOTA")
                .HasColumnType("xml")
                .IsRequired();

            entity.Property(e => e.IdPedido)
                .HasColumnName("ID_PEDIDO")
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.Consolidada)
                .HasColumnName("CONSOLIDADA")
                .IsRequired();
        });

        modelBuilder.Entity<ItensPedido>(entity =>
        {
            entity.ToTable("ItensPedido", "dbo");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.IdPedido)
                .HasColumnName("ID_PEDIDO");

            entity.Property(e => e.IdProduto)
                .HasColumnName("IdProduto");

            entity.Property(e => e.Codigo)
                .HasMaxLength(50);

            entity.Property(e => e.Descricao)
                .HasMaxLength(255);

            entity.Property(e => e.Unidade)
                .HasMaxLength(10);

            entity.Property(e => e.Quantidade)
                .HasPrecision(10, 2);

            entity.Property(e => e.ValorUnitario)
                .HasPrecision(10, 4);
        });

        modelBuilder.Entity<ConsolidadoVendas>(entity =>
        {
            entity.ToTable("CONSOLIDADO_VENDAS", "dbo");

            entity.HasKey(e => e.IdConsolidacao);

            entity.Property(e => e.IdConsolidacao)
                .HasColumnName("ID_CONSOLIDACAO")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.IdNota)
                .HasColumnName("ID_NOTA")
                .IsRequired();

            entity.Property(e => e.Cliente)
                .HasColumnName("CLIENTE")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.UfVenda)
                .HasColumnName("UF_VENDA")
                .HasMaxLength(2)
                .IsFixedLength()
                .IsRequired();

            entity.Property(e => e.ValorVenda)
                .HasColumnName("VALOR_VENDA")
                .HasPrecision(18, 2);

            entity.Property(e => e.TaxaMarketplace)
                .HasColumnName("TAXA_MARKETPLACE")
                .HasPrecision(18, 2);

            entity.Property(e => e.ValorFrete)
                .HasColumnName("VALOR_FRETE")
                .HasPrecision(18, 2);

            entity.Property(e => e.Icms)
                .HasColumnName("ICMS")
                .HasPrecision(18, 2);

            entity.Property(e => e.Pis)
                .HasColumnName("PIS")
                .HasPrecision(18, 2);

            entity.Property(e => e.Cofins)
                .HasColumnName("COFINS")
                .HasPrecision(18, 2);

            entity.Property(e => e.Difal)
                .HasColumnName("DIFAL")
                .HasPrecision(18, 2);
        });

        modelBuilder.Entity<Produto>(entity =>
        {
            entity.ToTable("PRODUTOS", "dbo");

            entity.HasKey(e => e.IdProduto);

            entity.Property(e => e.IdProduto)
                .HasColumnName("ID_PRODUTO")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.IdProdutoTiny)
                .HasColumnName("ID_PRODUTO_TINY")
                .HasMaxLength(10)
                .IsRequired();

            entity.Property(e => e.Nome)
                .HasColumnName("NOME")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.Preco)
                .HasColumnName("PRECO")
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(e => e.PrecoCusto)
                .HasColumnName("PRECO_CUSTO")
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(e => e.Ativo)
                .HasColumnName("ATIVO")
                .IsRequired();
        });

        modelBuilder.Entity<Sincronizacao>(entity =>
        {
            entity.ToTable("SINCRONIZACOES", "dbo");

            entity.HasKey(e => e.IdSincronizacao);

            entity.Property(e => e.IdSincronizacao)
                .HasColumnName("ID_SINCRONIZACAO")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.DataSincronizacao)
                .HasColumnName("DATA_SINCRONIZACAO")
                .IsRequired();

            entity.Property(e => e.Produtos)
                .HasColumnName("PRODUTOS")
                .IsRequired();

            entity.Property(e => e.Pedidos)
                .HasColumnName("PEDIDOS")
                .IsRequired();
        });
    }
}
