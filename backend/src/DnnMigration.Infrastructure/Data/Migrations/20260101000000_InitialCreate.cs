using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DnnMigration.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "aspnet_Profile",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastUpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PropertyNames = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PropertyValuesBinary = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    PropertyValuesString = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_aspnet_Profile", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "DesktopModules",
                columns: table => new
                {
                    DesktopModuleID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ModuleName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FriendlyName = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FolderName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Version = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsPremium = table.Column<bool>(type: "bit", nullable: false),
                    IsAdmin = table.Column<bool>(type: "bit", nullable: false),
                    BusinessControllerClass = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SupportedFeatures = table.Column<int>(type: "int", nullable: false),
                    CompatibleVersions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Dependencies = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Permissions = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DesktopModules", x => x.DesktopModuleID);
                });

            migrationBuilder.CreateTable(
                name: "ModuleDefinitions",
                columns: table => new
                {
                    ModuleDefID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FriendlyName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DesktopModuleID = table.Column<int>(type: "int", nullable: false),
                    DefaultCacheTime = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModuleDefinitions", x => x.ModuleDefID);
                });

            migrationBuilder.CreateTable(
                name: "Modules",
                columns: table => new
                {
                    ModuleID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PortalID = table.Column<int>(type: "int", nullable: false),
                    TabID = table.Column<int>(type: "int", nullable: false),
                    TabModuleID = table.Column<int>(type: "int", nullable: false),
                    ModuleDefID = table.Column<int>(type: "int", nullable: false),
                    ModuleOrder = table.Column<int>(type: "int", nullable: false),
                    PaneName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModuleTitle = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CacheTime = table.Column<int>(type: "int", nullable: false),
                    Alignment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Color = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Border = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IconFile = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AllTabs = table.Column<bool>(type: "bit", nullable: false),
                    Visibility = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Header = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Footer = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ContainerSrc = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DisplayTitle = table.Column<bool>(type: "bit", nullable: false),
                    DisplayPrint = table.Column<bool>(type: "bit", nullable: false),
                    DisplaySyndicate = table.Column<bool>(type: "bit", nullable: false),
                    InheritViewPermissions = table.Column<bool>(type: "bit", nullable: false),
                    ControlType = table.Column<int>(type: "int", nullable: false),
                    AuthorizedEditRoles = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AuthorizedViewRoles = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Modules", x => x.ModuleID);
                });

            migrationBuilder.CreateTable(
                name: "Permission",
                columns: table => new
                {
                    PermissionID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PermissionCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModuleDefID = table.Column<int>(type: "int", nullable: false),
                    PermissionKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PermissionName = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permission", x => x.PermissionID);
                });

            migrationBuilder.CreateTable(
                name: "Portals",
                columns: table => new
                {
                    PortalID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PortalName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LogoFile = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FooterText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserRegistration = table.Column<int>(type: "int", nullable: false),
                    BannerAdvertising = table.Column<int>(type: "int", nullable: false),
                    AdministratorId = table.Column<int>(type: "int", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HostFee = table.Column<float>(type: "real", nullable: false),
                    HostSpace = table.Column<int>(type: "int", nullable: false),
                    PageQuota = table.Column<int>(type: "int", nullable: false),
                    UserQuota = table.Column<int>(type: "int", nullable: false),
                    AdministratorRoleId = table.Column<int>(type: "int", nullable: false),
                    RegisteredRoleId = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    KeyWords = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BackgroundFile = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GUID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentProcessor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProcessorPassword = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProcessorUserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SiteLogHistory = table.Column<int>(type: "int", nullable: false),
                    AdminTabId = table.Column<int>(type: "int", nullable: false),
                    SplashTabId = table.Column<int>(type: "int", nullable: false),
                    HomeTabId = table.Column<int>(type: "int", nullable: false),
                    LoginTabId = table.Column<int>(type: "int", nullable: false),
                    UserTabId = table.Column<int>(type: "int", nullable: false),
                    DefaultLanguage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TimezoneOffset = table.Column<int>(type: "int", nullable: false),
                    HomeDirectory = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Portals", x => x.PortalID);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    RoleID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PortalID = table.Column<int>(type: "int", nullable: false),
                    RoleGroupID = table.Column<int>(type: "int", nullable: false),
                    RoleName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BillingFrequency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ServiceFee = table.Column<decimal>(type: "money", nullable: false),
                    TrialFrequency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TrialPeriod = table.Column<int>(type: "int", nullable: false),
                    BillingPeriod = table.Column<int>(type: "int", nullable: false),
                    TrialFee = table.Column<decimal>(type: "money", nullable: false),
                    IsPublic = table.Column<bool>(type: "bit", nullable: false),
                    AutoAssignment = table.Column<bool>(type: "bit", nullable: false),
                    RSVPCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IconFile = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.RoleID);
                });

            migrationBuilder.CreateTable(
                name: "Tabs",
                columns: table => new
                {
                    TabID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TabOrder = table.Column<int>(type: "int", nullable: false),
                    PortalID = table.Column<int>(type: "int", nullable: false),
                    TabName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsVisible = table.Column<bool>(type: "bit", nullable: false),
                    ParentId = table.Column<int>(type: "int", nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    IconFile = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DisableLink = table.Column<bool>(type: "bit", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    KeyWords = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SkinSrc = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContainerSrc = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TabPath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RefreshInterval = table.Column<int>(type: "int", nullable: false),
                    PageHeadText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsSecure = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tabs", x => x.TabID);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AffiliateId = table.Column<int>(type: "int", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsSuperUser = table.Column<bool>(type: "bit", nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PortalID = table.Column<int>(type: "int", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserID);
                });

            migrationBuilder.CreateTable(
                name: "FolderPermission",
                columns: table => new
                {
                    FolderPermissionID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FolderID = table.Column<int>(type: "int", nullable: false),
                    RoleID = table.Column<int>(type: "int", nullable: false),
                    AllowAccess = table.Column<bool>(type: "bit", nullable: false),
                    PermissionID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FolderPermission", x => x.FolderPermissionID);
                    table.ForeignKey(
                        name: "FK_FolderPermission_Permission",
                        column: x => x.PermissionID,
                        principalTable: "Permission",
                        principalColumn: "PermissionID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModulePermission",
                columns: table => new
                {
                    ModulePermissionID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ModuleID = table.Column<int>(type: "int", nullable: false),
                    RoleID = table.Column<int>(type: "int", nullable: false),
                    AllowAccess = table.Column<bool>(type: "bit", nullable: false),
                    PermissionID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModulePermission", x => x.ModulePermissionID);
                    table.ForeignKey(
                        name: "FK_ModulePermission_Modules",
                        column: x => x.ModuleID,
                        principalTable: "Modules",
                        principalColumn: "ModuleID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ModulePermission_Permission",
                        column: x => x.PermissionID,
                        principalTable: "Permission",
                        principalColumn: "PermissionID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PortalAlias",
                columns: table => new
                {
                    PortalAliasID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PortalID = table.Column<int>(type: "int", nullable: false),
                    HTTPAlias = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortalAlias", x => x.PortalAliasID);
                    table.ForeignKey(
                        name: "FK_PortalAlias_Portals",
                        column: x => x.PortalID,
                        principalTable: "Portals",
                        principalColumn: "PortalID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TabPermission",
                columns: table => new
                {
                    TabPermissionID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TabID = table.Column<int>(type: "int", nullable: false),
                    RoleID = table.Column<int>(type: "int", nullable: false),
                    AllowAccess = table.Column<bool>(type: "bit", nullable: false),
                    PermissionID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TabPermission", x => x.TabPermissionID);
                    table.ForeignKey(
                        name: "FK_TabPermission_Permission",
                        column: x => x.PermissionID,
                        principalTable: "Permission",
                        principalColumn: "PermissionID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TabPermission_Tabs",
                        column: x => x.TabID,
                        principalTable: "Tabs",
                        principalColumn: "TabID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "aspnet_Membership",
                columns: table => new
                {
                    UserID = table.Column<int>(type: "int", nullable: false),
                    IsApproved = table.Column<bool>(type: "bit", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastLockoutDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastLoginDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastPasswordChangedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsLockedOut = table.Column<bool>(type: "bit", nullable: false),
                    Password = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PasswordAnswer = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PasswordQuestion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_aspnet_Membership", x => x.UserID);
                    table.ForeignKey(
                        name: "FK_aspnet_Membership_Users_UserID",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DesktopModules",
                table: "DesktopModules",
                column: "FriendlyName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FolderPermission_PermissionID",
                table: "FolderPermission",
                column: "PermissionID");

            migrationBuilder.CreateIndex(
                name: "IX_ModulePermission_ModuleID",
                table: "ModulePermission",
                column: "ModuleID");

            migrationBuilder.CreateIndex(
                name: "IX_ModulePermission_PermissionID",
                table: "ModulePermission",
                column: "PermissionID");

            migrationBuilder.CreateIndex(
                name: "IX_PortalAlias_PortalID",
                table: "PortalAlias",
                column: "PortalID");

            migrationBuilder.CreateIndex(
                name: "IX_TabPermission_PermissionID",
                table: "TabPermission",
                column: "PermissionID");

            migrationBuilder.CreateIndex(
                name: "IX_TabPermission_TabID",
                table: "TabPermission",
                column: "TabID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "aspnet_Membership");

            migrationBuilder.DropTable(
                name: "aspnet_Profile");

            migrationBuilder.DropTable(
                name: "DesktopModules");

            migrationBuilder.DropTable(
                name: "FolderPermission");

            migrationBuilder.DropTable(
                name: "ModuleDefinitions");

            migrationBuilder.DropTable(
                name: "ModulePermission");

            migrationBuilder.DropTable(
                name: "PortalAlias");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "TabPermission");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Modules");

            migrationBuilder.DropTable(
                name: "Portals");

            migrationBuilder.DropTable(
                name: "Permission");

            migrationBuilder.DropTable(
                name: "Tabs");
        }
    }
}
