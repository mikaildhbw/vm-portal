using Microsoft.EntityFrameworkCore;
using VmPortal.Core.Data.Entities;

namespace VmPortal.Core.Data;

public class VmPortalDbContext : DbContext
{
    public VmPortalDbContext(DbContextOptions<VmPortalDbContext> options) : base(options)
    {
    }

    public DbSet<VirtualServer> VirtualServers => Set<VirtualServer>();
    public DbSet<VirtualMachineRecord> VirtualMachines => Set<VirtualMachineRecord>();
    public DbSet<VirtualMachineGroup> VirtualMachineGroups => Set<VirtualMachineGroup>();
    public DbSet<UserGroup> UserGroups => Set<UserGroup>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<VmActionEntity> VMActions => Set<VmActionEntity>();
    public DbSet<RoleAction> RoleActions => Set<RoleAction>();
    public DbSet<GroupPermission> GroupPermissions => Set<GroupPermission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<VirtualMachineRecord>(entity =>
        {
            entity.ToTable("VirtualMachines");
            entity.HasOne(vm => vm.Server)
                .WithMany()
                .HasForeignKey(vm => vm.ServerId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(vm => vm.Group)
                .WithMany(g => g.VirtualMachines)
                .HasForeignKey(vm => vm.GroupId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<VmActionEntity>(entity =>
        {
            entity.ToTable("VMActions");
            entity.HasIndex(a => a.Name).IsUnique();
        });

        modelBuilder.Entity<RoleAction>(entity =>
        {
            entity.HasKey(ra => new { ra.RoleId, ra.ActionId });
            entity.HasOne(ra => ra.Role)
                .WithMany(r => r.RoleActions)
                .HasForeignKey(ra => ra.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(ra => ra.Action)
                .WithMany(a => a.RoleActions)
                .HasForeignKey(ra => ra.ActionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GroupPermission>(entity =>
        {
            // Bewusst UNIQUE über alle drei Spalten statt nur (VmGroupId, UserGroupId):
            // erlaubt, dass ein UserGroup/VmGroup-Paar gleichzeitig mehrere Rollen hat.
            // Details siehe XML-Doc auf GroupPermission.
            entity.HasIndex(gp => new { gp.VmGroupId, gp.UserGroupId, gp.RoleId }).IsUnique();

            entity.HasOne(gp => gp.VmGroup)
                .WithMany(g => g.GroupPermissions)
                .HasForeignKey(gp => gp.VmGroupId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(gp => gp.UserGroup)
                .WithMany(ug => ug.GroupPermissions)
                .HasForeignKey(gp => gp.UserGroupId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(gp => gp.Role)
                .WithMany(r => r.GroupPermissions)
                .HasForeignKey(gp => gp.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Role>()
            .HasIndex(r => r.Name)
            .IsUnique();

        modelBuilder.Entity<UserGroup>()
            .HasIndex(ug => ug.Name)
            .IsUnique();

        modelBuilder.Entity<VirtualMachineGroup>()
            .HasIndex(g => g.Name)
            .IsUnique();

        modelBuilder.Entity<Role>().HasData(AuthorizationSeedData.Roles);
        modelBuilder.Entity<VmActionEntity>().HasData(AuthorizationSeedData.VMActions);
        modelBuilder.Entity<RoleAction>().HasData(AuthorizationSeedData.RoleActions);
        modelBuilder.Entity<VirtualServer>().HasData(AuthorizationSeedData.VirtualServers);
        modelBuilder.Entity<UserGroup>().HasData(AuthorizationSeedData.UserGroups);

        // Testberechtigung für den Verfasser-Account (ESXUserIT auf den HVP-Test-VMs) -
        // siehe Kommentar auf den Test*-Properties in AuthorizationSeedData.
        modelBuilder.Entity<UserGroup>().HasData(AuthorizationSeedData.TestUserGroups);
        modelBuilder.Entity<VirtualMachineGroup>().HasData(AuthorizationSeedData.TestVirtualMachineGroups);
        modelBuilder.Entity<VirtualMachineRecord>().HasData(AuthorizationSeedData.TestVirtualMachines);
        modelBuilder.Entity<GroupPermission>().HasData(AuthorizationSeedData.TestGroupPermissions);
    }
}
