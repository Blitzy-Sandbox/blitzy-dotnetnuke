'
' DotNetNuke® - http://www.dotnetnuke.com
' Copyright (c) 2002-2008
' by DotNetNuke Corporation
'
' Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated 
' documentation files (the "Software"), to deal in the Software without restriction, including without limitation 
' the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and 
' to permit persons to whom the Software is furnished to do so, subject to the following conditions:
'
' The above copyright notice and this permission notice shall be included in all copies or substantial portions 
' of the Software.
'
' THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED 
' TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL 
' THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF 
' CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
' DEALINGS IN THE SOFTWARE.
'

Imports System.Data.Common
Imports System

Namespace DotNetNuke.Data

    Public MustInherit Class DataProvider

#Region "Shared/Static Methods"
        ' provider constants - eliminates need for Reflection later
        Private Const ProviderType As String = "data"    ' maps to <sectionGroup> in web.config
        Private Const ProviderNamespace As String = "DotNetNuke.Data"    ' project namespace
        Private Const ProviderAssemblyName As String = ""    ' project assemblyname

        ' singleton reference to the instantiated object 
        Private Shared objProvider As DataProvider = Nothing

        ' constructor
        Shared Sub New()
            CreateProvider()
        End Sub

        ' dynamically create provider
        Private Shared Sub CreateProvider()
            objProvider = CType(Framework.Reflection.CreateObject(ProviderType, ProviderNamespace, ProviderAssemblyName), DataProvider)
        End Sub

        ' return the provider
        Public Shared Shadows Function Instance() As DataProvider
            Return objProvider
        End Function

#End Region

#Region "Abstract Methods"

        'Generic Methods
        '===============
        Public MustOverride Sub ExecuteNonQuery(ByVal ProcedureName As String, ByVal ParamArray commandParameters() As Object)
        Public MustOverride Function ExecuteReader(ByVal ProcedureName As String, ByVal ParamArray commandParameters() As Object) As IDataReader
        Public MustOverride Function ExecuteScalar(ByVal ProcedureName As String, ByVal ParamArray commandParameters() As Object) As Object
        Public MustOverride Function ExecuteScalar(Of T)(ByVal ProcedureName As String, ByVal ParamArray commandParameters() As Object) As T
        Public MustOverride Function ExecuteDataSet(ByVal ProcedureName As String, ByVal ParamArray commandParameters() As Object) As DataSet
        Public MustOverride Function ExecuteSQL(ByVal SQL As String) As IDataReader
        Public MustOverride Function ExecuteSQL(ByVal SQL As String, ByVal ParamArray commandParameters() As IDataParameter) As IDataReader

        ' general
        Public MustOverride Function GetConnectionStringBuilder() As DbConnectionStringBuilder
        Public MustOverride Function GetNull(ByVal Field As Object) As Object

        'transaction
        Public MustOverride Sub CommitTransaction(ByVal transaction As DbTransaction)
        Public MustOverride Overloads Function ExecuteScript(ByVal Script As String, ByVal transaction As DbTransaction) As String
        Public MustOverride Function GetTransaction() As DbTransaction
        Public MustOverride Sub RollbackTransaction(ByVal transaction As DbTransaction)

        ' upgrade
        Public MustOverride Function GetProviderPath() As String
        Public MustOverride Overloads Function ExecuteScript(ByVal SQL As String) As String
        Public MustOverride Overloads Function ExecuteScript(ByVal SQL As String, ByVal UseTransactions As Boolean) As String
        Public MustOverride Function GetDatabaseVersion() As IDataReader
        Public MustOverride Function TestDatabaseConnection(ByVal builder As DbConnectionStringBuilder, ByVal Owner As String, ByVal Qualifier As String) As String
        Public MustOverride Sub UpdateDatabaseVersion(ByVal Major As Integer, ByVal Minor As Integer, ByVal Build As Integer)
        Public MustOverride Function FindDatabaseVersion(ByVal Major As Integer, ByVal Minor As Integer, ByVal Build As Integer) As IDataReader
        Public MustOverride Sub UpgradeDatabaseSchema(ByVal Major As Integer, ByVal Minor As Integer, ByVal Build As Integer)

        ' host
        Public MustOverride Function GetHostSettings() As IDataReader
        Public MustOverride Function GetHostSetting(ByVal SettingName As String) As IDataReader
        Public MustOverride Sub AddHostSetting(ByVal SettingName As String, ByVal SettingValue As String, ByVal SettingIsSecure As Boolean)
        Public MustOverride Sub UpdateHostSetting(ByVal SettingName As String, ByVal SettingValue As String, ByVal SettingIsSecure As Boolean)

        ' portal
        Public MustOverride Function AddPortalInfo(ByVal PortalName As String, ByVal Currency As String, ByVal FirstName As String, ByVal LastName As String, ByVal Username As String, ByVal Password As String, ByVal Email As String, ByVal ExpiryDate As Date, ByVal HostFee As Double, ByVal HostSpace As Double, ByVal PageQuota As Integer, ByVal UserQuota As Integer, ByVal SiteLogHistory As Integer, ByVal HomeDirectory As String) As Integer
        Public MustOverride Function CreatePortal(ByVal PortalName As String, ByVal Currency As String, ByVal ExpiryDate As Date, ByVal HostFee As Double, ByVal HostSpace As Double, ByVal PageQuota As Integer, ByVal UserQuota As Integer, ByVal SiteLogHistory As Integer, ByVal HomeDirectory As String) As Integer
        Public MustOverride Sub DeletePortalInfo(ByVal PortalId As Integer)
        Public MustOverride Function GetExpiredPortals() As IDataReader
        Public MustOverride Function GetPortal(ByVal PortalId As Integer) As IDataReader
        Public MustOverride Function GetPortalByAlias(ByVal PortalAlias As String) As IDataReader
        Public MustOverride Function GetPortalByTab(ByVal TabId As Integer, ByVal PortalAlias As String) As IDataReader
        Public MustOverride Function GetPortalCount() As Integer
        Public MustOverride Function GetPortals() As IDataReader
        Public MustOverride Function GetPortalsByName(ByVal nameToMatch As String, ByVal pageIndex As Integer, ByVal pageSize As Integer) As IDataReader
        Public MustOverride Function GetPortalSpaceUsed(ByVal PortalId As Integer) As IDataReader
        Public MustOverride Sub UpdatePortalInfo(ByVal PortalId As Integer, ByVal PortalName As String, ByVal LogoFile As String, ByVal FooterText As String, ByVal ExpiryDate As Date, ByVal UserRegistration As Integer, ByVal BannerAdvertising As Integer, ByVal Currency As String, ByVal AdministratorId As Integer, ByVal HostFee As Double, ByVal HostSpace As Double, ByVal PageQuota As Integer, ByVal UserQuota As Integer, ByVal PaymentProcessor As String, ByVal ProcessorUserId As String, ByVal ProcessorPassword As String, ByVal Description As String, ByVal KeyWords As String, ByVal BackgroundFile As String, ByVal SiteLogHistory As Integer, ByVal SplashTabId As Integer, ByVal HomeTabId As Integer, ByVal LoginTabId As Integer, ByVal UserTabId As Integer, ByVal DefaultLanguage As String, ByVal TimeZoneOffset As Integer, ByVal HomeDirectory As String)
        Public MustOverride Sub UpdatePortalSetup(ByVal PortalId As Integer, ByVal AdministratorId As Integer, ByVal AdministratorRoleId As Integer, ByVal RegisteredRoleId As Integer, ByVal SplashTabId As Integer, ByVal HomeTabId As Integer, ByVal LoginTabId As Integer, ByVal UserTabId As Integer, ByVal AdminTabId As Integer)
        Public MustOverride Function VerifyPortalTab(ByVal PortalId As Integer, ByVal TabId As Integer) As IDataReader
        Public MustOverride Function VerifyPortal(ByVal PortalId As Integer) As IDataReader

        ' tab
        Public MustOverride Function AddTab(ByVal PortalId As Integer, ByVal TabName As String, ByVal IsVisible As Boolean, ByVal DisableLink As Boolean, ByVal ParentId As Integer, ByVal IconFile As String, ByVal Title As String, ByVal Description As String, ByVal KeyWords As String, ByVal Url As String, ByVal SkinSrc As String, ByVal ContainerSrc As String, ByVal TabPath As String, ByVal StartDate As Date, ByVal EndDate As Date, ByVal RefreshInterval As Integer, ByVal PageHeadText As String, ByVal IsSecure As Boolean) As Integer
        Public MustOverride Sub UpdateTab(ByVal TabId As Integer, ByVal TabName As String, ByVal IsVisible As Boolean, ByVal DisableLink As Boolean, ByVal ParentId As Integer, ByVal IconFile As String, ByVal Title As String, ByVal Description As String, ByVal KeyWords As String, ByVal IsDeleted As Boolean, ByVal Url As String, ByVal SkinSrc As String, ByVal ContainerSrc As String, ByVal TabPath As String, ByVal StartDate As Date, ByVal EndDate As Date)
        Public MustOverride Sub UpdateTab(ByVal TabId As Integer, ByVal TabName As String, ByVal IsVisible As Boolean, ByVal DisableLink As Boolean, ByVal ParentId As Integer, ByVal IconFile As String, ByVal Title As String, ByVal Description As String, ByVal KeyWords As String, ByVal IsDeleted As Boolean, ByVal Url As String, ByVal SkinSrc As String, ByVal ContainerSrc As String, ByVal TabPath As String, ByVal StartDate As Date, ByVal EndDate As Date, ByVal RefreshInterval As Integer, ByVal PageHeadText As String, ByVal IsSecure As Boolean)
        Public MustOverride Sub UpdateTabOrder(ByVal TabId As Integer, ByVal TabOrder As Integer, ByVal Level As Integer, ByVal ParentId As Integer, ByVal TabPath As String)
        Public MustOverride Sub DeleteTab(ByVal TabId As Integer)
        Public MustOverride Function GetTabs(ByVal PortalId As Integer) As IDataReader
        Public MustOverride Function GetAllTabs() As IDataReader
        Public MustOverride Function GetTab(ByVal TabId As Integer) As IDataReader
        Public MustOverride Function GetTabByName(ByVal TabName As String, ByVal PortalId As Integer) As IDataReader
        Public MustOverride Function GetTabsByParentId(ByVal ParentId As Integer) As IDataReader
        Public MustOverride Function GetTabCount(ByVal PortalId As Integer) As Integer
        Public MustOverride Function GetPortalTabModules(ByVal PortalId As Integer, ByVal TabId As Integer) As IDataReader
        Public MustOverride Function GetTabModules(ByVal TabId As Integer) As IDataReader
        Public MustOverride Function GetTabPanes(ByVal TabId As Integer) As IDataReader

        ' module
        Public MustOverride Function GetAllModules() As IDataReader
        Public MustOverride Function GetModules(ByVal PortalId As Integer) As IDataReader
        Public MustOverride Function GetAllTabsModules(ByVal PortalId As Integer, ByVal AllTabs As Boolean) As IDataReader
        Public MustOverride Function GetModule(ByVal ModuleId As Integer, ByVal TabId As Integer) As IDataReader
        Public MustOverride Function GetModuleByDefinition(ByVal PortalId As Integer, ByVal FriendlyName As String) As IDataReader
        Public MustOverride Function GetSearchModules(ByVal PortalId As Integer) As IDataReader
        Public MustOverride Function AddModule(ByVal PortalID As Integer, ByVal ModuleDefID As Integer, ByVal ModuleTitle As String, ByVal AllTabs As Boolean, ByVal Header As String, ByVal Footer As String, ByVal StartDate As DateTime, ByVal EndDate As DateTime, ByVal InheritViewPermissions As Boolean, ByVal IsDeleted As Boolean) As Integer
        Public MustOverride Sub UpdateModule(ByVal ModuleId As Integer, ByVal ModuleTitle As String, ByVal AllTabs As Boolean, ByVal Header As String, ByVal Footer As String, ByVal StartDate As DateTime, ByVal EndDate As DateTime, ByVal InheritViewPermissions As Boolean, ByVal IsDeleted As Boolean)
        Public MustOverride Sub DeleteModule(ByVal ModuleId As Integer)
        Public MustOverride Function GetTabModuleOrder(ByVal TabId As Integer, ByVal PaneName As String) As IDataReader
        Public MustOverride Sub UpdateModuleOrder(ByVal TabId As Integer, ByVal ModuleId As Integer, ByVal ModuleOrder As Integer, ByVal PaneName As String)

        Public MustOverride Sub AddTabModule(ByVal TabId As Integer, ByVal ModuleId As Integer, ByVal ModuleOrder As Integer, ByVal PaneName As String, ByVal CacheTime As Integer, ByVal Alignment As String, ByVal Color As String, ByVal Border As String, ByVal IconFile As String, ByVal Visibility As Integer, ByVal ContainerSrc As String, ByVal DisplayTitle As Boolean, ByVal DisplayPrint As Boolean, ByVal DisplaySyndicate As Boolean)
        Public MustOverride Sub UpdateTabModule(ByVal TabId As Integer, ByVal ModuleId As Integer, ByVal ModuleOrder As Integer, ByVal PaneName As String, ByVal CacheTime As Integer, ByVal Alignment As String, ByVal Color As String, ByVal Border As String, ByVal IconFile As String, ByVal Visibility As Integer, ByVal ContainerSrc As String, ByVal DisplayTitle As Boolean, ByVal DisplayPrint As Boolean, ByVal DisplaySyndicate As Boolean)
        Public MustOverride Sub DeleteTabModule(ByVal TabId As Integer, ByVal ModuleId As Integer)

        Public MustOverride Function GetModuleSettings(ByVal ModuleId As Integer) As IDataReader
        Public MustOverride Function GetModuleSetting(ByVal ModuleId As Integer, ByVal SettingName As String) As IDataReader
        Public MustOverride Sub AddModuleSetting(ByVal ModuleId As Integer, ByVal SettingName As String, ByVal SettingValue As String)
        Public MustOverride Sub UpdateModuleSetting(ByVal ModuleId As Integer, ByVal SettingName As String, ByVal SettingValue As String)
        Public MustOverride Sub DeleteModuleSetting(ByVal ModuleId As Integer, ByVal SettingName As String)
        Public MustOverride Sub DeleteModuleSettings(ByVal ModuleId As Integer)

        Public MustOverride Function GetTabModuleSettings(ByVal TabModuleId As Integer) As IDataReader
        Public MustOverride Function GetTabModuleSetting(ByVal TabModuleId As Integer, ByVal SettingName As String) As IDataReader
        Public MustOverride Sub AddTabModuleSetting(ByVal TabModuleId As Integer, ByVal SettingName As String, ByVal SettingValue As String)
        Public MustOverride Sub UpdateTabModuleSetting(ByVal TabModuleId As Integer, ByVal SettingName As String, ByVal SettingValue As String)
        Public MustOverride Sub DeleteTabModuleSetting(ByVal TabModuleId As Integer, ByVal SettingName As String)
        Public MustOverride Sub DeleteTabModuleSettings(ByVal TabModuleId As Integer)

        ' module definition
        Public MustOverride Function GetDesktopModule(ByVal DesktopModuleId As Integer) As IDataReader
        Public MustOverride Function GetDesktopModuleByFriendlyName(ByVal FriendlyName As String) As IDataReader
        Public MustOverride Function GetDesktopModuleByModuleName(ByVal ModuleName As String) As IDataReader
        Public MustOverride Function GetDesktopModules() As IDataReader
        Public MustOverride Function GetDesktopModulesByPortal(ByVal PortalID As Integer) As IDataReader
        Public MustOverride Function AddDesktopModule(ByVal ModuleName As String, ByVal FolderName As String, ByVal FriendlyName As String, ByVal Description As String, ByVal Version As String, ByVal IsPremium As Boolean, ByVal IsAdmin As Boolean, ByVal BusinessControllerClass As String, ByVal SupportedFeatures As Integer, ByVal CompatibleVersions As String, ByVal Dependencies As String, ByVal Permissions As String) As Integer
        Public MustOverride Sub UpdateDesktopModule(ByVal DesktopModuleId As Integer, ByVal ModuleName As String, ByVal FolderName As String, ByVal FriendlyName As String, ByVal Description As String, ByVal Version As String, ByVal IsPremium As Boolean, ByVal IsAdmin As Boolean, ByVal BusinessControllerClass As String, ByVal SupportedFeatures As Integer, ByVal CompatibleVersions As String, ByVal Dependencies As String, ByVal Permissions As String)
        Public MustOverride Sub DeleteDesktopModule(ByVal DesktopModuleId As Integer)

        Public MustOverride Function GetPortalDesktopModules(ByVal PortalID As Integer, ByVal DesktopModuleID As Integer) As IDataReader
        Public MustOverride Function AddPortalDesktopModule(ByVal PortalID As Integer, ByVal DesktopModuleID As Integer) As Integer
        Public MustOverride Sub DeletePortalDesktopModules(ByVal PortalID As Integer, ByVal DesktopModuleID As Integer)

        Public MustOverride Function GetModuleDefinitions(ByVal DesktopModuleId As Integer) As IDataReader
        Public MustOverride Function GetModuleDefinition(ByVal ModuleDefId As Integer) As IDataReader
        Public MustOverride Function GetModuleDefinitionByName(ByVal DesktopModuleId As Integer, ByVal FriendlyName As String) As IDataReader
        Public MustOverride Function AddModuleDefinition(ByVal DesktopModuleId As Integer, ByVal FriendlyName As String, ByVal DefaultCacheTime As Integer) As Integer
        Public MustOverride Sub DeleteModuleDefinition(ByVal ModuleDefId As Integer)
        Public MustOverride Sub UpdateModuleDefinition(ByVal ModuleDefId As Integer, ByVal FriendlyName As String, ByVal DefaultCacheTime As Integer)

        Public MustOverride Function GetModuleControl(ByVal ModuleControlId As Integer) As IDataReader
        Public MustOverride Function GetModuleControls(ByVal ModuleDefID As Integer) As IDataReader
        Public MustOverride Function GetModuleControlsByKey(ByVal ControlKey As String, ByVal ModuleDefId As Integer) As IDataReader
        Public MustOverride Function GetModuleControlByKeyAndSrc(ByVal ModuleDefID As Integer, ByVal ControlKey As String, ByVal ControlSrc As String) As IDataReader
        Public MustOverride Function AddModuleControl(ByVal ModuleDefId As Integer, ByVal ControlKey As String, ByVal ControlTitle As String, ByVal ControlSrc As String, ByVal IconFile As String, ByVal ControlType As Integer, ByVal ViewOrder As Integer, ByVal HelpUrl As String, ByVal SupportsPartialRendering As Boolean) As Integer
        Public MustOverride Sub UpdateModuleControl(ByVal ModuleControlId As Integer, ByVal ModuleDefId As Integer, ByVal ControlKey As String, ByVal ControlTitle As String, ByVal ControlSrc As String, ByVal IconFile As String, ByVal ControlType As Integer, ByVal ViewOrder As Integer, ByVal HelpUrl As String, ByVal SupportsPartialRendering As Boolean)
        Public MustOverride Sub DeleteModuleControl(ByVal ModuleControlId As Integer)

        ' files
        Public MustOverride Function GetFiles(ByVal PortalId As Integer, ByVal FolderID As Integer) As IDataReader
        Public MustOverride Function GetFile(ByVal FileName As String, ByVal PortalId As Integer, ByVal FolderID As Integer) As IDataReader
        Public MustOverride Function GetFileById(ByVal FileId As Integer, ByVal PortalId As Integer) As IDataReader
        Public MustOverride Sub DeleteFile(ByVal PortalId As Integer, ByVal FileName As String, ByVal FolderID As Integer)
        Public MustOverride Sub DeleteFiles(ByVal PortalId As Integer)
        Public MustOverride Function AddFile(ByVal PortalId As Integer, ByVal FileName As String, ByVal Extension As String, ByVal Size As Long, ByVal Width As Integer, ByVal Height As Integer, ByVal ContentType As String, ByVal Folder As String, ByVal FolderID As Integer) As Integer
        Public MustOverride Sub UpdateFile(ByVal FileId As Integer, ByVal FileName As String, ByVal Extension As String, ByVal Size As Long, ByVal Width As Integer, ByVal Height As Integer, ByVal ContentType As String, ByVal Folder As String, ByVal FolderID As Integer)
        Public MustOverride Function GetAllFiles() As DataTable
        Public MustOverride Function GetFileContent(ByVal FileId As Integer, ByVal PortalId As Integer) As IDataReader
        Public MustOverride Sub UpdateFileContent(ByVal FileId As Integer, ByVal StreamFile() As Byte)

        ' site log
        Public MustOverride Sub AddSiteLog(ByVal DateTime As Date, ByVal PortalId As Integer, ByVal UserId As Integer, ByVal Referrer As String, ByVal URL As String, ByVal UserAgent As String, ByVal UserHostAddress As String, ByVal UserHostName As String, ByVal TabId As Integer, ByVal AffiliateId As Integer)
        Public MustOverride Function GetSiteLogReports() As IDataReader
        Public MustOverride Function GetSiteLog(ByVal PortalId As Integer, ByVal PortalAlias As String, ByVal ReportName As String, ByVal StartDate As Date, ByVal EndDate As Date) As IDataReader
        Public MustOverride Sub DeleteSiteLog(ByVal DateTime As Date, ByVal PortalId As Integer)

        ' database 
        Public MustOverride Function GetTables() As IDataReader
        Public MustOverride Function GetFields(ByVal TableName As String) As IDataReader
        ' vendors
        Public MustOverride Function GetVendors(ByVal PortalId As Integer, ByVal UnAuthorized As Boolean, ByVal PageIndex As Integer, ByVal PageSize As Integer) As IDataReader
        Public MustOverride Function GetVendorsByEmail(ByVal Filter As String, ByVal PortalId As Integer, ByVal PageIndex As Integer, ByVal PageSize As Integer) As IDataReader
        Public MustOverride Function GetVendorsByName(ByVal Filter As String, ByVal PortalId As Integer, ByVal PageIndex As Integer, ByVal PageSize As Integer) As IDataReader
        Public MustOverride Function GetVendor(ByVal VendorID As Integer, ByVal PortalID As Integer) As IDataReader
        Public MustOverride Sub DeleteVendor(ByVal VendorID As Integer)
        Public MustOverride Function AddVendor(ByVal PortalID As Integer, ByVal VendorName As String, ByVal Unit As String, ByVal Street As String, ByVal City As String, ByVal Region As String, ByVal Country As String, ByVal PostalCode As String, ByVal Telephone As String, ByVal Fax As String, ByVal Cell As String, ByVal Email As String, ByVal Website As String, ByVal FirstName As String, ByVal LastName As String, ByVal UserName As String, ByVal LogoFile As String, ByVal KeyWords As String, ByVal Authorized As String) As Integer
        Public MustOverride Sub UpdateVendor(ByVal VendorID As Integer, ByVal VendorName As String, ByVal Unit As String, ByVal Street As String, ByVal City As String, ByVal Region As String, ByVal Country As String, ByVal PostalCode As String, ByVal Telephone As String, ByVal Fax As String, ByVal Cell As String, ByVal Email As String, ByVal Website As String, ByVal FirstName As String, ByVal LastName As String, ByVal UserName As String, ByVal LogoFile As String, ByVal KeyWords As String, ByVal Authorized As String)
        Public MustOverride Function GetVendorClassifications(ByVal VendorId As Integer) As IDataReader
        Public MustOverride Sub DeleteVendorClassifications(ByVal VendorId As Integer)
        Public MustOverride Function AddVendorClassification(ByVal VendorId As Integer, ByVal ClassificationId As Integer) As Integer

        ' banners
        Public MustOverride Function GetBanners(ByVal VendorId As Integer) As IDataReader
        Public MustOverride Function GetBanner(ByVal BannerId As Integer, ByVal VendorId As Integer, ByVal PortalID As Integer) As IDataReader
        Public MustOverride Function GetBannerGroups(ByVal PortalId As Integer) As DataTable
        Public MustOverride Sub DeleteBanner(ByVal BannerId As Integer)
        Public MustOverride Function AddBanner(ByVal BannerName As String, ByVal VendorId As Integer, ByVal ImageFile As String, ByVal URL As String, ByVal Impressions As Integer, ByVal CPM As Double, ByVal StartDate As Date, ByVal EndDate As Date, ByVal UserName As String, ByVal BannerTypeId As Integer, ByVal Description As String, ByVal GroupName As String, ByVal Criteria As Integer, ByVal Width As Integer, ByVal Height As Integer) As Integer
        Public MustOverride Sub UpdateBanner(ByVal BannerId As Integer, ByVal BannerName As String, ByVal ImageFile As String, ByVal URL As String, ByVal Impressions As Integer, ByVal CPM As Double, ByVal StartDate As Date, ByVal EndDate As Date, ByVal UserName As String, ByVal BannerTypeId As Integer, ByVal Description As String, ByVal GroupName As String, ByVal Criteria As Integer, ByVal Width As Integer, ByVal Height As Integer)
        Public MustOverride Function FindBanners(ByVal PortalId As Integer, ByVal BannerTypeId As Integer, ByVal GroupName As String) As IDataReader
        Public MustOverride Sub UpdateBannerViews(ByVal BannerId As Integer, ByVal StartDate As Date, ByVal EndDate As Date)
        Public MustOverride Sub UpdateBannerClickThrough(ByVal BannerId As Integer, ByVal VendorId As Integer)

        ' affiliates
        Public MustOverride Function GetAffiliates(ByVal VendorId As Integer) As IDataReader
        Public MustOverride Function GetAffiliate(ByVal AffiliateId As Integer, ByVal VendorId As Integer, ByVal PortalID As Integer) As IDataReader
        Public MustOverride Sub DeleteAffiliate(ByVal AffiliateId As Integer)
        Public MustOverride Function AddAffiliate(ByVal VendorId As Integer, ByVal StartDate As Date, ByVal EndDate As Date, ByVal CPC As Double, ByVal CPA As Double) As Integer
        Public MustOverride Sub UpdateAffiliate(ByVal AffiliateId As Integer, ByVal StartDate As Date, ByVal EndDate As Date, ByVal CPC As Double, ByVal CPA As Double)
        Public MustOverride Sub UpdateAffiliateStats(ByVal AffiliateId As Integer, ByVal Clicks As Integer, ByVal Acquisitions As Integer)

        ' skins/containers
        Public MustOverride Function GetSkin(ByVal SkinRoot As String, ByVal PortalId As Integer, ByVal SkinType As Integer) As IDataReader
        Public MustOverride Function GetSkins(ByVal PortalId As Integer) As IDataReader
        Public MustOverride Sub DeleteSkin(ByVal SkinRoot As String, ByVal PortalId As Integer, ByVal SkinType As Integer)
        Public MustOverride Function AddSkin(ByVal SkinRoot As String, ByVal PortalId As Integer, ByVal SkinType As Integer, ByVal SkinSrc As String) As Integer
        Public MustOverride Function CanDeleteSkin(ByVal SkinType As String, ByVal SkinFoldername As String) As Boolean

        ' personalization
        Public MustOverride Function GetAllProfiles() As IDataReader
        Public MustOverride Function GetProfile(ByVal UserId As Integer, ByVal PortalId As Integer) As IDataReader
        Public MustOverride Sub AddProfile(ByVal UserId As Integer, ByVal PortalId As Integer)
        Public MustOverride Sub UpdateProfile(ByVal UserId As Integer, ByVal PortalId As Integer, ByVal ProfileData As String)

        'profile property definitions
        Public MustOverride Function AddPropertyDefinition(ByVal PortalId As Integer, ByVal ModuleDefId As Integer, ByVal DataType As Integer, ByVal DefaultValue As String, ByVal PropertyCategory As String, ByVal PropertyName As String, ByVal Required As Boolean, ByVal ValidationExpression As String, ByVal ViewOrder As Integer, ByVal Visible As Boolean, ByVal Length As Integer) As Integer
        Public MustOverride Sub DeletePropertyDefinition(ByVal definitionId As Integer)
        Public MustOverride Function GetPropertyDefinition(ByVal definitionId As Integer) As IDataReader
        Public MustOverride Function GetPropertyDefinitionByName(ByVal portalId As Integer, ByVal name As String) As IDataReader
        Public MustOverride Function GetPropertyDefinitionsByPortal(ByVal portalId As Integer) As IDataReader
        Public MustOverride Sub UpdatePropertyDefinition(ByVal PropertyDefinitionId As Integer, ByVal DataType As Integer, ByVal DefaultValue As String, ByVal PropertyCategory As String, ByVal PropertyName As String, ByVal Required As Boolean, ByVal ValidationExpression As String, ByVal ViewOrder As Integer, ByVal Visible As Boolean, ByVal Length As Integer)

        ' urls
        Public MustOverride Function GetUrls(ByVal PortalID As Integer) As IDataReader
        Public MustOverride Function GetUrl(ByVal PortalID As Integer, ByVal Url As String) As IDataReader
        Public MustOverride Sub AddUrl(ByVal PortalID As Integer, ByVal Url As String)
        Public MustOverride Sub DeleteUrl(ByVal PortalID As Integer, ByVal Url As String)
        Public MustOverride Function GetUrlTracking(ByVal PortalID As Integer, ByVal Url As String, ByVal ModuleId As Integer) As IDataReader
        Public MustOverride Sub AddUrlTracking(ByVal PortalID As Integer, ByVal Url As String, ByVal UrlType As String, ByVal LogActivity As Boolean, ByVal TrackClicks As Boolean, ByVal ModuleId As Integer, ByVal NewWindow As Boolean)
        Public MustOverride Sub UpdateUrlTracking(ByVal PortalID As Integer, ByVal Url As String, ByVal LogActivity As Boolean, ByVal TrackClicks As Boolean, ByVal ModuleId As Integer, ByVal NewWindow As Boolean)
        Public MustOverride Sub DeleteUrlTracking(ByVal PortalID As Integer, ByVal Url As String, ByVal ModuleId As Integer)
        Public MustOverride Sub UpdateUrlTrackingStats(ByVal PortalID As Integer, ByVal Url As String, ByVal ModuleId As Integer)
        Public MustOverride Function GetUrlLog(ByVal UrlTrackingID As Integer, ByVal StartDate As Date, ByVal EndDate As Date) As IDataReader
        Public MustOverride Sub AddUrlLog(ByVal UrlTrackingID As Integer, ByVal UserID As Integer)

        'Folders
        Public MustOverride Function GetFoldersByPortal(ByVal PortalID As Integer) As IDataReader
        Public MustOverride Function GetFolder(ByVal PortalID As Integer, ByVal FolderID As Integer) As IDataReader
        Public MustOverride Function GetFolder(ByVal PortalID As Integer, ByVal FolderPath As String) As IDataReader
        Public MustOverride Function AddFolder(ByVal PortalID As Integer, ByVal FolderPath As String, ByVal StorageLocation As Integer, ByVal IsProtected As Boolean, ByVal IsCached As Boolean, ByVal LastUpdated As Date) As Integer
        Public MustOverride Sub UpdateFolder(ByVal PortalID As Integer, ByVal FolderID As Integer, ByVal FolderPath As String, ByVal StorageLocation As Integer, ByVal IsProtected As Boolean, ByVal IsCached As Boolean, ByVal LastUpdated As Date)
        Public MustOverride Sub DeleteFolder(ByVal PortalID As Integer, ByVal FolderPath As String)

        'Permission
        Public MustOverride Function GetPermission(ByVal permissionID As Integer) As IDataReader
        Public MustOverride Function GetPermissionsByModuleDefID(ByVal ModuleDefID As Integer) As IDataReader
        Public MustOverride Function GetPermissionsByModuleID(ByVal ModuleID As Integer) As IDataReader
        Public MustOverride Function GetPermissionsByFolderPath(ByVal PortalID As Integer, ByVal Folder As String) As IDataReader
        Public MustOverride Function GetPermissionByCodeAndKey(ByVal PermissionCode As String, ByVal PermissionKey As String) As IDataReader
        Public MustOverride Function GetPermissionsByTabID(ByVal TabID As Integer) As IDataReader
        Public MustOverride Sub DeletePermission(ByVal permissionID As Integer)
        Public MustOverride Function AddPermission(ByVal permissionCode As String, ByVal moduleDefID As Integer, ByVal permissionKey As String, ByVal permissionName As String) As Integer
        Public MustOverride Sub UpdatePermission(ByVal permissionID As Integer, ByVal permissionCode As String, ByVal moduleDefID As Integer, ByVal permissionKey As String, ByVal permissionName As String)

        'ModulePermission
        Public MustOverride Function GetModulePermission(ByVal modulePermissionID As Integer) As IDataReader
        Public MustOverride Function GetModulePermissionsByModuleID(ByVal moduleID As Integer, ByVal PermissionID As Integer) As IDataReader
        Public MustOverride Function GetModulePermissionsByPortal(ByVal PortalID As Integer) As IDataReader
        Public MustOverride Function GetModulePermissionsByTabID(ByVal TabID As Integer) As IDataReader
        Public MustOverride Sub DeleteModulePermissionsByModuleID(ByVal ModuleID As Integer)
        Public MustOverride Sub DeleteModulePermissionsByUserID(ByVal PortalID As Integer, ByVal UserID As Integer)
        Public MustOverride Sub DeleteModulePermission(ByVal modulePermissionID As Integer)
        Public MustOverride Function AddModulePermission(ByVal moduleID As Integer, ByVal PermissionID As Integer, ByVal roleID As Integer, ByVal AllowAccess As Boolean, ByVal UserID As Integer) As Integer
        Public MustOverride Sub UpdateModulePermission(ByVal modulePermissionID As Integer, ByVal moduleID As Integer, ByVal PermissionID As Integer, ByVal roleID As Integer, ByVal AllowAccess As Boolean, ByVal UserID As Integer)

        'TabPermission
        Public MustOverride Function GetTabPermissionsByPortal(ByVal PortalID As Integer) As IDataReader
        Public MustOverride Function GetTabPermissionsByTabID(ByVal TabID As Integer, ByVal PermissionID As Integer) As IDataReader
        Public MustOverride Sub DeleteTabPermissionsByTabID(ByVal TabID As Integer)
        Public MustOverride Sub DeleteTabPermissionsByUserID(ByVal PortalID As Integer, ByVal UserID As Integer)
        Public MustOverride Sub DeleteTabPermission(ByVal TabPermissionID As Integer)
        Public MustOverride Function AddTabPermission(ByVal TabID As Integer, ByVal PermissionID As Integer, ByVal roleID As Integer, ByVal AllowAccess As Boolean, ByVal UserID As Integer) As Integer
        Public MustOverride Sub UpdateTabPermission(ByVal TabPermissionID As Integer, ByVal TabID As Integer, ByVal PermissionID As Integer, ByVal roleID As Integer, ByVal AllowAccess As Boolean, ByVal UserID As Integer)

        'FolderPermission
        Public MustOverride Function GetFolderPermission(ByVal FolderPermissionID As Integer) As IDataReader
        Public MustOverride Function GetFolderPermissionsByPortal(ByVal PortalID As Integer) As IDataReader
        Public MustOverride Function GetFolderPermissionsByFolderPath(ByVal PortalID As Integer, ByVal FolderPath As String, ByVal PermissionID As Integer) As IDataReader
        Public MustOverride Sub DeleteFolderPermissionsByFolderPath(ByVal PortalID As Integer, ByVal FolderPath As String)
        Public MustOverride Sub DeleteFolderPermissionsByUserID(ByVal PortalID As Integer, ByVal UserID As Integer)
        Public MustOverride Sub DeleteFolderPermission(ByVal FolderPermissionID As Integer)
        Public MustOverride Function AddFolderPermission(ByVal FolderID As Integer, ByVal PermissionID As Integer, ByVal roleID As Integer, ByVal AllowAccess As Boolean, ByVal UserID As Integer) As Integer
        Public MustOverride Sub UpdateFolderPermission(ByVal FolderPermissionID As Integer, ByVal FolderID As Integer, ByVal PermissionID As Integer, ByVal roleID As Integer, ByVal AllowAccess As Boolean, ByVal UserID As Integer)

        ' search engine
        Public MustOverride Function GetSearchIndexers() As IDataReader
        Public MustOverride Function GetSearchResultModules(ByVal PortalID As Integer) As System.Data.IDataReader

        ' content search datastore
        Public MustOverride Sub DeleteSearchItems(ByVal ModuleID As Integer)
        Public MustOverride Sub DeleteSearchItem(ByVal SearchItemId As Integer)
        Public MustOverride Sub DeleteSearchItemWords(ByVal SearchItemId As Integer)
        Public MustOverride Function AddSearchItem(ByVal Title As String, ByVal Description As String, ByVal Author As Integer, ByVal PubDate As Date, ByVal ModuleId As Integer, ByVal Key As String, ByVal Guid As String, ByVal ImageFileId As Integer) As Integer
        Public MustOverride Function GetSearchCommonWordsByLocale(ByVal Locale As String) As IDataReader
        Public MustOverride Function GetDefaultLanguageByModule(ByVal ModuleList As String) As IDataReader
        Public MustOverride Function GetSearchSettings(ByVal ModuleId As Integer) As IDataReader
        Public MustOverride Function GetSearchWords() As IDataReader
        Public MustOverride Function AddSearchWord(ByVal Word As String) As Integer
        Public MustOverride Function AddSearchItemWord(ByVal SearchItemId As Integer, ByVal SearchWordsID As Integer, ByVal Occurrences As Integer) As Integer
        Public MustOverride Sub AddSearchItemWordPosition(ByVal SearchItemWordID As Integer, ByVal ContentPositions As String)
        Public MustOverride Function GetSearchResults(ByVal PortalID As Integer, ByVal Word As String) As IDataReader
        Public MustOverride Function GetSearchItems(ByVal PortalID As Integer, ByVal TabID As Integer, ByVal ModuleID As Integer) As IDataReader
        Public MustOverride Function GetSearchItem(ByVal ModuleID As Integer, ByVal SearchKey As String) As IDataReader
        Public MustOverride Sub UpdateSearchItem(ByVal SearchItemId As Integer, ByVal Title As String, ByVal Description As String, ByVal Author As Integer, ByVal PubDate As Date, ByVal ModuleId As Integer, ByVal Key As String, ByVal Guid As String, ByVal HitCount As Integer, ByVal ImageFileId As Integer)

        'Lists
        Public MustOverride Function GetLists(ByVal PortalID As Integer) As IDataReader
        Public MustOverride Function GetList(ByVal ListName As String, ByVal ParentKey As String, ByVal PortalID As Integer) As IDataReader
        Public MustOverride Function GetListEntry(ByVal EntryID As Integer) As IDataReader
        Public MustOverride Function GetListEntry(ByVal ListName As String, ByVal Value As String) As IDataReader
        Public MustOverride Function GetListEntriesByListName(ByVal ListName As String, ByVal ParentKey As String) As IDataReader
        Public MustOverride Function AddListEntry(ByVal ListName As String, ByVal Value As String, ByVal Text As String, ByVal ParentID As Integer, ByVal Level As Integer, ByVal EnableSortOrder As Boolean, ByVal DefinitionID As Integer, ByVal Description As String, ByVal PortalID As Integer) As Integer
        Public MustOverride Sub UpdateListEntry(ByVal EntryID As Integer, ByVal Value As String, ByVal Text As String, ByVal Description As String)
        Public MustOverride Sub DeleteListEntryByID(ByVal EntryID As Integer, ByVal DeleteChild As Boolean)
        Public MustOverride Sub DeleteList(ByVal ListName As String, ByVal ParentKey As String)
        Public MustOverride Sub DeleteListEntryByListName(ByVal ListName As String, ByVal Value As String, ByVal DeleteChild As Boolean)
        Public MustOverride Sub UpdateListSortOrder(ByVal EntryID As Integer, ByVal MoveUp As Boolean)

        'portal alias
        Public MustOverride Function GetPortalAlias(ByVal PortalAlias As String, ByVal PortalID As Integer) As IDataReader
        Public MustOverride Function GetPortalAliasByPortalID(ByVal PortalID As Integer) As IDataReader
        Public MustOverride Function GetPortalAliasByPortalAliasID(ByVal PortalAliasID As Integer) As IDataReader
        Public MustOverride Function GetPortalByPortalAliasID(ByVal PortalAliasId As Integer) As IDataReader
        Public MustOverride Sub UpdatePortalAlias(ByVal PortalAlias As String)
        Public MustOverride Sub UpdatePortalAliasInfo(ByVal PortalAliasID As Integer, ByVal PortalID As Integer, ByVal HTTPAlias As String)
        Public MustOverride Function AddPortalAlias(ByVal PortalID As Integer, ByVal HTTPAlias As String) As Integer
        Public MustOverride Sub DeletePortalAlias(ByVal PortalAliasID As Integer)

        'event Queue
        Public MustOverride Function AddEventMessage(ByVal eventName As String, ByVal priority As Integer, ByVal processorType As String, ByVal processorCommand As String, ByVal body As String, ByVal sender As String, ByVal subscriberId As String, ByVal authorizedRoles As String, ByVal exceptionMessage As String, ByVal sentDate As Date, ByVal expirationDate As Date, ByVal attributes As String) As Integer
        Public MustOverride Function GetEventMessages(ByVal eventName As String) As IDataReader
        Public MustOverride Function GetEventMessagesBySubscriber(ByVal eventName As String, ByVal subscriberId As String) As IDataReader
        Public MustOverride Sub SetEventMessageComplete(ByVal eventMessageId As Integer)

        'Authentication
        Public MustOverride Function AddAuthentication(ByVal packageID As Integer, ByVal authenticationType As String, ByVal isEnabled As Boolean, ByVal settingsControlSrc As String, ByVal loginControlSrc As String, ByVal logoffControlSrc As String) As Integer
        Public MustOverride Function AddUserAuthentication(ByVal userID As Integer, ByVal authenticationType As String, ByVal authenticationToken As String) As Integer
        Public MustOverride Sub DeleteAuthentication(ByVal authenticationID As Integer)
        Public MustOverride Function GetAuthenticationService(ByVal authenticationID As Integer) As IDataReader
        Public MustOverride Function GetAuthenticationServiceByPackageID(ByVal packageID As Integer) As IDataReader
        Public MustOverride Function GetAuthenticationServiceByType(ByVal authenticationType As String) As IDataReader
        Public MustOverride Function GetAuthenticationServices() As IDataReader
        Public MustOverride Function GetEnabledAuthenticationServices() As IDataReader
        Public MustOverride Sub UpdateAuthentication(ByVal authenticationID As Integer, ByVal packageID As Integer, ByVal authenticationType As String, ByVal isEnabled As Boolean, ByVal settingsControlSrc As String, ByVal loginControlSrc As String, ByVal logoffControlSrc As String)

        'Packages
        Public MustOverride Function AddPackage(ByVal name As String, ByVal friendlyName As String, ByVal description As String, ByVal type As String, ByVal version As String, ByVal license As String, ByVal manifest As String) As Integer
        Public MustOverride Sub DeletePackage(ByVal packageID As Integer)
        Public MustOverride Function GetPackage(ByVal packageID As Integer) As IDataReader
        Public MustOverride Function GetPackageByName(ByVal name As String) As IDataReader
        Public MustOverride Function GetPackages() As IDataReader
        Public MustOverride Function GetPackagesByType(ByVal type As String) As IDataReader
        Public MustOverride Function GetPackageType(ByVal type As String) As IDataReader
        Public MustOverride Function RegisterAssembly(ByVal packageID As Integer, ByVal assemblyName As String, ByVal version As String) As Integer
        Public MustOverride Function UnRegisterAssembly(ByVal packageID As Integer, ByVal assemblyName As String) As Boolean
        Public MustOverride Sub UpdatePackage(ByVal name As String, ByVal friendlyName As String, ByVal description As String, ByVal type As String, ByVal version As String, ByVal license As String, ByVal manifest As String)

#End Region

    End Class

End Namespace
