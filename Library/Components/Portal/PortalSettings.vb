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

Imports System
Imports System.Collections.Generic
Imports System.Configuration
Imports System.Data
Imports System.Web
Imports System.Collections
Imports System.IO
Imports System.Web.UI

Imports DotNetNuke.Entities.Modules
Imports DotNetNuke.Entities.Modules.Definitions
Imports DotNetNuke.Entities.Tabs
Imports DotNetNuke.Services.Tokens
Imports DotNetNuke.UI.Skins

Namespace DotNetNuke.Entities.Portals

    ''' -----------------------------------------------------------------------------
    ''' <summary>
    ''' PortalSettings Class
    '''
    ''' This class encapsulates all of the settings for the Portal, as well
    ''' as the configuration settings required to execute the current tab
    ''' view within the portal.
    ''' </summary>
    ''' <remarks>
    ''' </remarks>
    ''' <history>
    ''' 	[cnurse]	10/21/2004	documented
    ''' 	[cnurse]	10/21/2004	added GetTabModuleSettings
    ''' </history>
    ''' -----------------------------------------------------------------------------
    Public Class PortalSettings
        Implements IPropertyAccess

#Region "Enums"

        Public Enum Mode
            View
            Edit
        End Enum

        Public Enum ControlPanelPermission
            TabEditor
            ModuleEditor
        End Enum

#End Region

#Region "Private Members"

        Private _PortalId As Integer
        Private _PortalName As String
        Private _HomeDirectory As String
        Private _LogoFile As String
        Private _FooterText As String
        Private _ExpiryDate As Date
        Private _UserRegistration As Integer
        Private _BannerAdvertising As Integer
        Private _Currency As String
        Private _AdministratorId As Integer
        Private _Email As String
        Private _HostFee As Single
        Private _HostSpace As Integer
        Private _PageQuota As Integer
        Private _UserQuota As Integer
        Private _AdministratorRoleId As Integer
        Private _AdministratorRoleName As String
        Private _RegisteredRoleId As Integer
        Private _RegisteredRoleName As String
        Private _Description As String
        Private _KeyWords As String
        Private _BackgroundFile As String
        Private _GUID As Guid
        Private _SiteLogHistory As Integer
        Private _AdminTabId As Integer
        Private _SuperTabId As Integer
        Private _SplashTabId As Integer
        Private _HomeTabId As Integer
        Private _LoginTabId As Integer
        Private _UserTabId As Integer
        Private _DefaultLanguage As String
        Private _TimeZoneOffset As Integer
        Private _Version As String
        Private _DesktopTabs As ArrayList
        Private _ActiveTab As TabInfo
        Private _PortalAlias As PortalAliasInfo
        Private _AdminContainer As SkinInfo
        Private _AdminSkin As SkinInfo
        Private _PortalContainer As SkinInfo
        Private _PortalSkin As SkinInfo
        Private _Users As Integer
        Private _Pages As Integer

#End Region

#Region "Public Properties"

        Public Property PortalId() As Integer
            Get
                Return _PortalId
            End Get
            Set(ByVal Value As Integer)
                _PortalId = Value
            End Set
        End Property
        Public Property PortalName() As String
            Get
                Return _PortalName
            End Get
            Set(ByVal Value As String)
                _PortalName = Value
            End Set
        End Property
        Public Property HomeDirectory() As String
            Get
                Return _HomeDirectory
            End Get
            Set(ByVal Value As String)
                _HomeDirectory = Value
            End Set
        End Property
        Public ReadOnly Property HomeDirectoryMapPath() As String
            Get
                Dim objFolderController As New Services.FileSystem.FolderController
                Return objFolderController.GetMappedDirectory(HomeDirectory)
            End Get

        End Property
        Public Property LogoFile() As String
            Get
                Return _LogoFile
            End Get
            Set(ByVal Value As String)
                _LogoFile = Value
            End Set
        End Property
        Public Property FooterText() As String
            Get
                Return _FooterText
            End Get
            Set(ByVal Value As String)
                _FooterText = Value
            End Set
        End Property
        Public Property ExpiryDate() As Date
            Get
                Return _ExpiryDate
            End Get
            Set(ByVal Value As Date)
                _ExpiryDate = Value
            End Set
        End Property
        Public Property UserRegistration() As Integer
            Get
                Return _UserRegistration
            End Get
            Set(ByVal Value As Integer)
                _UserRegistration = Value
            End Set
        End Property
        Public Property BannerAdvertising() As Integer
            Get
                Return _BannerAdvertising
            End Get
            Set(ByVal Value As Integer)
                _BannerAdvertising = Value
            End Set
        End Property
        Public Property Currency() As String
            Get
                Return _Currency
            End Get
            Set(ByVal Value As String)
                _Currency = Value
            End Set
        End Property
        Public Property AdministratorId() As Integer
            Get
                Return _AdministratorId
            End Get
            Set(ByVal Value As Integer)
                _AdministratorId = Value
            End Set
        End Property
        Public Property Email() As String
            Get
                Return _Email
            End Get
            Set(ByVal Value As String)
                _Email = Value
            End Set
        End Property
        Public Property HostFee() As Single
            Get
                Return _HostFee
            End Get
            Set(ByVal Value As Single)
                _HostFee = Value
            End Set
        End Property
        Public Property HostSpace() As Integer
            Get
                Return _HostSpace
            End Get
            Set(ByVal Value As Integer)
                _HostSpace = Value
            End Set
        End Property
        Public Property PageQuota() As Integer
            Get
                Return _PageQuota
            End Get
            Set(ByVal Value As Integer)
                _PageQuota = Value
            End Set
        End Property
        Public Property UserQuota() As Integer
            Get
                Return _UserQuota
            End Get
            Set(ByVal Value As Integer)
                _UserQuota = Value
            End Set
        End Property
        Public Property AdministratorRoleId() As Integer
            Get
                Return _AdministratorRoleId
            End Get
            Set(ByVal Value As Integer)
                _AdministratorRoleId = Value
            End Set
        End Property
        Public Property AdministratorRoleName() As String
            Get
                Return _AdministratorRoleName
            End Get
            Set(ByVal Value As String)
                _AdministratorRoleName = Value
            End Set
        End Property
        Public Property RegisteredRoleId() As Integer
            Get
                Return _RegisteredRoleId
            End Get
            Set(ByVal Value As Integer)
                _RegisteredRoleId = Value
            End Set
        End Property
        Public Property RegisteredRoleName() As String
            Get
                Return _RegisteredRoleName
            End Get
            Set(ByVal Value As String)
                _RegisteredRoleName = Value
            End Set
        End Property
        Public Property Description() As String
            Get
                Return _Description
            End Get
            Set(ByVal Value As String)
                _Description = Value
            End Set
        End Property
        Public Property KeyWords() As String
            Get
                Return _KeyWords
            End Get
            Set(ByVal Value As String)
                _KeyWords = Value
            End Set
        End Property
        Public Property BackgroundFile() As String
            Get
                Return _BackgroundFile
            End Get
            Set(ByVal Value As String)
                _BackgroundFile = Value
            End Set
        End Property
        Public Property GUID() As Guid
            Get
                Return _GUID
            End Get
            Set(ByVal Value As Guid)
                _GUID = Value
            End Set
        End Property
        Public Property SiteLogHistory() As Integer
            Get
                Return _SiteLogHistory
            End Get
            Set(ByVal Value As Integer)
                _SiteLogHistory = Value
            End Set
        End Property
        Public Property AdminTabId() As Integer
            Get
                Return _AdminTabId
            End Get
            Set(ByVal Value As Integer)
                _AdminTabId = Value
            End Set
        End Property
        Public Property SuperTabId() As Integer
            Get
                Return _SuperTabId
            End Get
            Set(ByVal Value As Integer)
                _SuperTabId = Value
            End Set
        End Property
        Public Property SplashTabId() As Integer
            Get
                Return _SplashTabId
            End Get
            Set(ByVal Value As Integer)
                _SplashTabId = Value
            End Set
        End Property
        Public Property HomeTabId() As Integer
            Get
                Return _HomeTabId
            End Get
            Set(ByVal Value As Integer)
                _HomeTabId = Value
            End Set
        End Property
        Public Property LoginTabId() As Integer
            Get
                Return _LoginTabId
            End Get
            Set(ByVal Value As Integer)
                _LoginTabId = Value
            End Set
        End Property
        Public Property UserTabId() As Integer
            Get
                Return _UserTabId
            End Get
            Set(ByVal Value As Integer)
                _UserTabId = Value
            End Set
        End Property
        Public Property DefaultLanguage() As String
            Get
                Return _DefaultLanguage
            End Get
            Set(ByVal Value As String)
                _DefaultLanguage = Value
            End Set
        End Property
        Public Property TimeZoneOffset() As Integer
            Get
                Return _TimeZoneOffset
            End Get
            Set(ByVal Value As Integer)
                _TimeZoneOffset = Value
            End Set
        End Property
        Public Property Version() As String
            Get
                Return _Version
            End Get
            Set(ByVal Value As String)
                _Version = Value
            End Set
        End Property
        Public Property DesktopTabs() As ArrayList
            Get
                Return _DesktopTabs
            End Get
            Set(ByVal Value As ArrayList)
                _DesktopTabs = Value
            End Set
        End Property
        Public Property ActiveTab() As TabInfo
            Get
                Return _ActiveTab
            End Get
            Set(ByVal Value As TabInfo)
                _ActiveTab = Value
            End Set
        End Property
        Public ReadOnly Property HostSettings() As Hashtable
            Get
                Return Common.Globals.HostSettings    '_HostSettings
            End Get
        End Property
        Public Property PortalAlias() As PortalAliasInfo
            Get
                Return _PortalAlias
            End Get
            Set(ByVal Value As PortalAliasInfo)
                _PortalAlias = Value
            End Set
        End Property
        Public Property AdminContainer() As SkinInfo
            Get
                Return _AdminContainer
            End Get
            Set(ByVal value As SkinInfo)
                _AdminContainer = value
            End Set
        End Property
        Public Property AdminSkin() As SkinInfo
            Get
                Return _AdminSkin
            End Get
            Set(ByVal value As SkinInfo)
                _AdminSkin = value
            End Set
        End Property
        Public Property PortalContainer() As SkinInfo
            Get
                Return _PortalContainer
            End Get
            Set(ByVal value As SkinInfo)
                _PortalContainer = value
            End Set
        End Property
        Public Property PortalSkin() As SkinInfo
            Get
                Return _PortalSkin
            End Get
            Set(ByVal value As SkinInfo)
                _PortalSkin = value
            End Set
        End Property
        Public Property Users() As Integer
            Get
                Return _Users
            End Get
            Set(ByVal Value As Integer)
                _Users = Value
            End Set
        End Property
        Public Property Pages() As Integer
            Get
                Return _Pages
            End Get
            Set(ByVal Value As Integer)
                _Pages = Value
            End Set
        End Property

        Public ReadOnly Property ContentVisible() As Boolean
            Get
                If HttpContext.Current.Request.IsAuthenticated Then
                    Dim Content As String = Convert.ToString(Personalization.Personalization.GetProfile("Usability", "ContentVisible" & PortalId.ToString))
                    If Content = "" Then
                        Return True
                    Else
                        Return Convert.ToBoolean(Content)
                    End If
                Else
                    Return True
                End If
            End Get
        End Property

        Public ReadOnly Property ControlPanelVisible() As Boolean
            Get
                Dim Visible As String = Convert.ToString(Personalization.Personalization.GetProfile("Usability", "ControlPanelVisible" & PortalId.ToString))
                If Visible = "" Then
                    If Convert.ToString(Entities.Portals.PortalSettings.GetSiteSetting(PortalId, "ControlPanelVisibility")) <> "MIN" Then
                        Return True
                    Else
                        Return False
                    End If
                Else
                    Return Convert.ToBoolean(Visible)
                End If
            End Get
        End Property

        Public ReadOnly Property UserMode() As Mode
            Get
                If HttpContext.Current.Request.IsAuthenticated Then
                    Dim Mode As String = Convert.ToString(Personalization.Personalization.GetProfile("Usability", "UserMode" & PortalId.ToString))
                    Select Case Mode
                        Case ""
                            If Convert.ToString(Entities.Portals.PortalSettings.GetSiteSetting(PortalId, "ControlPanelMode")) <> "VIEW" Then
                                Return PortalSettings.Mode.Edit
                            Else
                                Return PortalSettings.Mode.View
                            End If
                        Case "View"
                            Return PortalSettings.Mode.View
                        Case "Edit"
                            Return PortalSettings.Mode.Edit
                    End Select
                Else
                    Return PortalSettings.Mode.View
                End If
            End Get
        End Property

        Public ReadOnly Property ControlPanelSecurity() As ControlPanelPermission
            Get
                If Convert.ToString(Entities.Portals.PortalSettings.GetSiteSetting(PortalId, "ControlPanelSecurity")) <> "TAB" Then
                    Return ControlPanelPermission.ModuleEditor
                Else
                    Return ControlPanelPermission.TabEditor
                End If
            End Get
        End Property

#End Region

#Region "Constructors"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The PortalSettings Constructor encapsulates all of the logic
        ''' necessary to obtain configuration settings necessary to render
        ''' a Portal Tab view for a given request.
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="tabId">The current tab</param>
        '''	<param name="objPortalAliasInfo">The current portal</param>
        ''' <history>
        ''' 	[cnurse]	10/21/2004	documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub New(ByVal tabId As Integer, ByVal objPortalAliasInfo As PortalAliasInfo)
            _DesktopTabs = New ArrayList
            _ActiveTab = New TabInfo
            GetPortalSettings(tabId, objPortalAliasInfo)
        End Sub

        Public Sub New()
        End Sub

#End Region

#Region "Private Methods"

        Private Sub GetBreadCrumbsRecursively(ByRef objBreadCrumbs As ArrayList, ByVal intTabId As Integer)

            ' find the tab in the desktoptabs collection
            Dim blnFound As Boolean = False
            Dim objTab As TabInfo = Nothing
            For Each objTab In Me.DesktopTabs
                If objTab.TabID = intTabId Then
                    blnFound = True
                    Exit For
                End If
            Next

            ' if tab was found
            If blnFound Then
                ' add tab to breadcrumb collection
                objBreadCrumbs.Insert(0, objTab.Clone)

                ' get the tab parent
                If Not Null.IsNull(objTab.ParentId) Then
                    GetBreadCrumbsRecursively(objBreadCrumbs, objTab.ParentId)
                End If
            End If

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The GetPortalSettings method builds the site Settings
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="TabId">The current tabs id</param>
        '''	<param name="objPortalAliasInfo">The Portal Alias object</param>
        ''' <history>
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub GetPortalSettings(ByVal TabId As Integer, ByVal objPortalAliasInfo As PortalAliasInfo)
            Dim objPortals As New PortalController
            Dim objPortal As PortalInfo
            Dim objTabs As New TabController
            Dim objModules As New ModuleController
            Dim objModule As ModuleInfo
            Dim objSkin As UI.Skins.SkinInfo

            PortalId = objPortalAliasInfo.PortalID

            ' get portal settings
            objPortal = objPortals.GetPortal(PortalId)
            If Not objPortal Is Nothing Then
                Me.PortalAlias = objPortalAliasInfo
                Me.PortalId = objPortal.PortalID
                Me.PortalName = objPortal.PortalName
                Me.LogoFile = objPortal.LogoFile
                Me.FooterText = objPortal.FooterText
                Me.ExpiryDate = objPortal.ExpiryDate
                Me.UserRegistration = objPortal.UserRegistration
                Me.BannerAdvertising = objPortal.BannerAdvertising
                Me.Currency = objPortal.Currency
                Me.AdministratorId = objPortal.AdministratorId
                Me.Email = objPortal.Email
                Me.HostFee = objPortal.HostFee
                Me.HostSpace = objPortal.HostSpace
                Me.PageQuota = objPortal.PageQuota
                Me.UserQuota = objPortal.UserQuota
                Me.AdministratorRoleId = objPortal.AdministratorRoleId
                Me.AdministratorRoleName = objPortal.AdministratorRoleName
                Me.RegisteredRoleId = objPortal.RegisteredRoleId
                Me.RegisteredRoleName = objPortal.RegisteredRoleName
                Me.Description = objPortal.Description
                Me.KeyWords = objPortal.KeyWords
                Me.BackgroundFile = objPortal.BackgroundFile
                Me.GUID = objPortal.GUID
                Me.SiteLogHistory = objPortal.SiteLogHistory
                Me.AdminTabId = objPortal.AdminTabId
                Me.SuperTabId = objPortal.SuperTabId
                Me.SplashTabId = objPortal.SplashTabId
                Me.HomeTabId = objPortal.HomeTabId
                Me.LoginTabId = objPortal.LoginTabId
                Me.UserTabId = objPortal.UserTabId
                Me.DefaultLanguage = objPortal.DefaultLanguage
                Me.TimeZoneOffset = objPortal.TimeZoneOffset
                Me.HomeDirectory = objPortal.HomeDirectory
                Me.Version = objPortal.Version
                Me.AdminSkin = SkinController.GetSkin(SkinInfo.RootSkin, PortalId, SkinType.Admin)
                If Me.AdminSkin Is Nothing Then
                    Me.AdminSkin = SkinController.GetSkin(SkinInfo.RootSkin, Null.NullInteger, SkinType.Admin)
                End If
                Me.PortalSkin = SkinController.GetSkin(SkinInfo.RootSkin, PortalId, SkinType.Portal)
                If Me.PortalSkin Is Nothing Then
                    Me.PortalSkin = SkinController.GetSkin(SkinInfo.RootSkin, Null.NullInteger, SkinType.Portal)
                End If
                Me.AdminContainer = SkinController.GetSkin(SkinInfo.RootContainer, PortalId, SkinType.Admin)
                If Me.AdminContainer Is Nothing Then
                    Me.AdminContainer = SkinController.GetSkin(SkinInfo.RootContainer, Null.NullInteger, SkinType.Admin)
                End If
                Me.PortalContainer = SkinController.GetSkin(SkinInfo.RootContainer, PortalId, SkinType.Portal)
                If Me.PortalContainer Is Nothing Then
                    Me.PortalContainer = SkinController.GetSkin(SkinInfo.RootContainer, Null.NullInteger, SkinType.Portal)
                End If
                Me.Pages = objPortal.Pages
                Me.Users = objPortal.Users

                ' set custom properties
                If Null.IsNull(Me.HostSpace) Then
                    Me.HostSpace = 0
                End If
                If Null.IsNull(Me.DefaultLanguage) Then
                    Me.DefaultLanguage = Localization.SystemLocale
                End If
                If Null.IsNull(Me.TimeZoneOffset) Then
                    Me.TimeZoneOffset = Localization.SystemTimeZoneOffset
                End If
                Me.HomeDirectory = Common.Globals.ApplicationPath + "/" + objPortal.HomeDirectory + "/"

                ' get application version
                Dim arrVersion As Array = glbAppVersion.Split(CType(".", Char))
                Dim intMajor As Integer = CType(arrVersion.GetValue((0)), Integer)
                Dim intMinor As Integer = CType(arrVersion.GetValue((1)), Integer)
                Dim intBuild As Integer = CType(arrVersion.GetValue((2)), Integer)
                Me.Version = intMajor.ToString + "." + intMinor.ToString + "." + intBuild.ToString

            End If

            'Add each portal Tab to DekstopTabs
            Dim objPortalTab As TabInfo
            For Each tabPair As KeyValuePair(Of Integer, TabInfo) In objTabs.GetTabsByPortal(Me.PortalId)
                ' clone the tab object ( to avoid creating an object reference to the data cache )
                objPortalTab = tabPair.Value.Clone()

                ' set custom properties
                If objPortalTab.TabOrder = 0 Then
                    objPortalTab.TabOrder = 999
                End If
                If Null.IsNull(objPortalTab.StartDate) Then
                    objPortalTab.StartDate = Date.MinValue
                End If
                If Null.IsNull(objPortalTab.EndDate) Then
                    objPortalTab.EndDate = Date.MaxValue
                End If

                Me.DesktopTabs.Add(objPortalTab)
            Next

            'Add each host Tab to DesktopTabs
            Dim objHostTab As TabInfo
            For Each tabPair As KeyValuePair(Of Integer, TabInfo) In objTabs.GetTabsByPortal(Null.NullInteger)
                ' clone the tab object ( to avoid creating an object reference to the data cache )
                objHostTab = tabPair.Value.Clone()
                objHostTab.PortalID = Me.PortalId
                objHostTab.StartDate = Date.MinValue
                objHostTab.EndDate = Date.MaxValue

                Me.DesktopTabs.Add(objHostTab)
            Next

            'At this point the DesktopTabs Collection contains all the Tabs for the current portal
            'verify tab for portal. This assigns the Active Tab based on the Tab Id/PortalId
            If VerifyPortalTab(PortalId, TabId) Then
                If Not Me.ActiveTab Is Nothing Then
                    ' skin
                    If Me.ActiveTab.SkinSrc = "" Then
                        If IsAdminSkin(Me.ActiveTab.IsAdminTab) Then
                            objSkin = Me.AdminSkin
                        Else
                            objSkin = Me.PortalSkin
                        End If
                        If Not objSkin Is Nothing Then
                            Me.ActiveTab.SkinSrc = objSkin.SkinSrc
                        End If
                    End If
                    If Me.ActiveTab.SkinSrc = "" Then
                        If IsAdminSkin(Me.ActiveTab.IsAdminTab) Then
                            Me.ActiveTab.SkinSrc = "[G]" & SkinInfo.RootSkin & DefaultSkin.Folder & DefaultSkin.AdminDefaultName
                        Else
                            Me.ActiveTab.SkinSrc = "[G]" & SkinInfo.RootSkin & DefaultSkin.Folder & DefaultSkin.DefaultName
                        End If
                    End If
                    Me.ActiveTab.SkinSrc = SkinController.FormatSkinSrc(Me.ActiveTab.SkinSrc, Me)
                    Me.ActiveTab.SkinPath = SkinController.FormatSkinPath(Me.ActiveTab.SkinSrc)
                    ' container
                    If Me.ActiveTab.ContainerSrc = "" Then
                        If IsAdminSkin(Me.ActiveTab.IsAdminTab) Then
                            objSkin = Me.AdminContainer
                        Else
                            objSkin = Me.PortalContainer
                        End If
                        If Not objSkin Is Nothing Then
                            Me.ActiveTab.ContainerSrc = objSkin.SkinSrc
                        End If
                    End If
                    If Me.ActiveTab.ContainerSrc = "" Then
                        If IsAdminSkin(Me.ActiveTab.IsAdminTab) Then
                            Me.ActiveTab.ContainerSrc = "[G]" & SkinInfo.RootContainer & DefaultContainer.Folder & DefaultContainer.AdminDefaultName
                        Else
                            Me.ActiveTab.ContainerSrc = "[G]" & SkinInfo.RootContainer & DefaultContainer.Folder & DefaultContainer.DefaultName
                        End If
                    End If
                    Me.ActiveTab.ContainerSrc = SkinController.FormatSkinSrc(Me.ActiveTab.ContainerSrc, Me)
                    Me.ActiveTab.ContainerPath = SkinController.FormatSkinPath(Me.ActiveTab.ContainerSrc)

                    ' initialize collections
                    Me.ActiveTab.BreadCrumbs = New ArrayList
                    Me.ActiveTab.Panes = New ArrayList
                    Me.ActiveTab.Modules = New ArrayList

                    ' get breadcrumbs for current tab
                    GetBreadCrumbsRecursively(Me.ActiveTab.BreadCrumbs, Me.ActiveTab.TabID)
                End If
            End If

            Dim objPaneModules As New Hashtable

            ' get current tab modules
            For Each kvp As KeyValuePair(Of Integer, ModuleInfo) In objModules.GetTabModules(Me.ActiveTab.TabID)
                objModule = kvp.Value

                ' clone the module object ( to avoid creating an object reference to the data cache )
                Dim cloneModule As ModuleInfo = objModule.Clone

                ' set custom properties
                If Null.IsNull(cloneModule.StartDate) Then
                    cloneModule.StartDate = Date.MinValue
                End If
                If Null.IsNull(cloneModule.EndDate) Then
                    cloneModule.EndDate = Date.MaxValue
                End If
                ' container
                If cloneModule.ContainerSrc = "" Then
                    cloneModule.ContainerSrc = Me.ActiveTab.ContainerSrc
                End If
                cloneModule.ContainerSrc = SkinController.FormatSkinSrc(cloneModule.ContainerSrc, Me)
                cloneModule.ContainerPath = SkinController.FormatSkinPath(cloneModule.ContainerSrc)

                ' process tab panes
                If objPaneModules.ContainsKey(cloneModule.PaneName) = False Then
                    objPaneModules.Add(cloneModule.PaneName, 0)
                End If
                cloneModule.PaneModuleCount = 0
                If Not cloneModule.IsDeleted Then
                    objPaneModules(cloneModule.PaneName) = CType(objPaneModules(cloneModule.PaneName), Integer) + 1
                    cloneModule.PaneModuleIndex = CType(objPaneModules(cloneModule.PaneName), Integer) - 1
                End If

                Me.ActiveTab.Modules.Add(cloneModule)
            Next

            ' set pane module count
            For Each objModule In Me.ActiveTab.Modules
                objModule.PaneModuleCount = Convert.ToInt32(objPaneModules(objModule.PaneName))
            Next

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The VerifyPortalTab method verifies that the TabId/PortalId combination
        ''' is allowed and returns default/home tab ids if not
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="PortalId">The Portal's id</param>
        '''	<param name="TabId">The current tab's id</param>
        ''' <history>
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Function VerifyPortalTab(ByVal PortalId As Integer, ByVal TabId As Integer) As Boolean

            Dim objTab As TabInfo = Nothing
            Dim objSplashTab As TabInfo = Nothing
            Dim objHomeTab As TabInfo = Nothing
            Dim isVerified As Boolean = False
            Dim objTabs As New TabController

            ' find the tab in the desktoptabs collection
            If TabId <> Null.NullInteger Then
                For Each objTab In Me.DesktopTabs
                    If objTab.TabID = TabId Then
                        'Check if Tab has been deleted (is in recycle bin)
                        If Not (objTab.IsDeleted) Then
                            Me.ActiveTab = objTab.Clone()
                            isVerified = True
                            Exit For
                        End If
                    End If
                Next
            End If

            ' if tab was not found 
            If Not isVerified And Me.SplashTabId > 0 Then
                ' use the splash tab ( if specified )
                objSplashTab = objTabs.GetTab(Me.SplashTabId, PortalId, False)
                Me.ActiveTab = objSplashTab.Clone()
                isVerified = True
            End If

            ' if tab was not found 
            If Not isVerified And Me.HomeTabId > 0 Then
                ' use the home tab ( if specified )
                objHomeTab = objTabs.GetTab(Me.HomeTabId, PortalId, False)
                Me.ActiveTab = objHomeTab.Clone()
                isVerified = True
            End If

            ' if tab was not found 
            If Not isVerified Then
                ' get the first tab in the collection (that is valid)
                For i As Integer = 0 To Me.DesktopTabs.Count - 1
                    objTab = CType(Me.DesktopTabs(i), TabInfo)
                    'Check if Tab has not been deleted (not in recycle bin) and is visible
                    If Not (objTab.IsDeleted) And objTab.IsVisible Then
                        Me.ActiveTab = objTab.Clone()
                        isVerified = True
                        Exit For
                    End If
                Next
            End If

            If Null.IsNull(Me.ActiveTab.StartDate) Then
                Me.ActiveTab.StartDate = Date.MinValue
            End If
            If Null.IsNull(Me.ActiveTab.EndDate) Then
                Me.ActiveTab.EndDate = Date.MaxValue
            End If

            Return isVerified

        End Function

#End Region

#Region "Public Shared Methods"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The GetHostSettings method returns a hashtable of
        ''' host settings from the database.
        ''' </summary>
        ''' <returns>A Hashtable of settings (key/value pairs)</returns>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetHostSettings() As Hashtable
            Return Common.Globals.HostSettings
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The GetSiteSettings method returns a hashtable of
        ''' portal specific settings from the database.  This method 
        ''' uses the Site Settings module as a convenient storage area for
        ''' portal-wide settings.
        ''' </summary>
        ''' <returns>A Hashtable of settings (key/value pairs)</returns>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="PortalId">The Portal</param>
        ''' <history>
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetSiteSettings(ByVal PortalId As Integer) As Hashtable

            Dim objModules As New ModuleController

            Dim ModuleId As Integer = objModules.GetModuleByDefinition(PortalId, "Site Settings").ModuleID

            Return GetModuleSettings(ModuleId)

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The GetSiteSetting method returns a 
        ''' portal specific setting from the database.  This method 
        ''' uses the Site Settings module as a convenient storage area for
        ''' portal-wide settings.
        ''' </summary>
        ''' <returns>A Hashtable of settings (key/value pairs)</returns>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="PortalId">The Portal</param>
        '''	<param name="SettingName">The Setting</param>
        ''' <history>
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetSiteSetting(ByVal PortalId As Integer, ByVal SettingName As String) As String

            Dim settings As Hashtable = GetSiteSettings(PortalId)

            Return Convert.ToString(settings(SettingName))

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The UpdateSiteSetting method updates a specific portal setting
        ''' in the database. Since this is a portal-wide storage area you must
        ''' be careful to avoid naming collisions on SettingNames.
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="PortalId">The Portal</param>
        '''	<param name="SettingName">The Setting Name</param>
        '''	<param name="SettingValue">The Setting Value</param>
        ''' <history>
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub UpdateSiteSetting(ByVal PortalId As Integer, ByVal SettingName As String, ByVal SettingValue As String)

            Dim objModules As New ModuleController

            Dim ModuleId As Integer = objModules.GetModuleByDefinition(PortalId, "Site Settings").ModuleID

            objModules.UpdateModuleSetting(ModuleId, SettingName, SettingValue)

        End Sub

        <Obsolete("This method has been deprecated.  Please use UpdateSiteSetting(PortalId, SettingName, SettingValue)")> _
        Public Shared Sub UpdatePortalSetting(ByVal PortalId As Integer, ByVal SettingName As String, ByVal SettingValue As String)
            UpdateSiteSetting(PortalId, SettingName, SettingValue)
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The GetModuleSettings Method returns a hashtable of
        ''' custom module specific settings from the database.  This method is
        ''' used by some user control modules (Xml, Image, etc) to access misc
        ''' settings.
        ''' </summary>
        ''' <returns>A Hashtable of settings (key/value pairs)</returns>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="ModuleId">The Module</param>
        ''' <history>
        ''' 	[cnurse]	10/21/2004	documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetModuleSettings(ByVal ModuleId As Integer) As Hashtable

            Dim objModules As New ModuleController

            Return objModules.GetModuleSettings(ModuleId)

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The GetTabModuleSettings Method returns a hashtable of
        ''' custom module/tab specific settings from the database.  This method is
        ''' used by some user control modules (Xml, Image, etc) to access misc
        ''' settings.
        ''' </summary>
        ''' <returns>A Hashtable of settings (key/value pairs)</returns>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="TabModuleId">The current tabModule</param>
        ''' <history>
        ''' 	[cnurse]	10/21/2004	documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetTabModuleSettings(ByVal TabModuleId As Integer) As Hashtable

            Dim objModules As New ModuleController

            Return objModules.GetTabModuleSettings(TabModuleId)

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The GetTabModuleSettings Method returns a hashtable of
        ''' custom module/tab specific settings from the database.  This method is
        ''' used by some user control modules (Xml, Image, etc) to access misc
        ''' settings.
        ''' </summary>
        ''' <returns>A Hashtable of settings (key/value pairs)</returns>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="TabModuleId">The current tabmodule</param>
        '''	<param name="settings">A Hashtable to add the Settings to</param>
        ''' <history>
        ''' 	[cnurse]	10/21/2004	documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetTabModuleSettings(ByVal TabModuleId As Integer, ByVal settings As Hashtable) As Hashtable

            Return GetTabModuleSettings(New Hashtable(settings), New Hashtable(GetTabModuleSettings(TabModuleId)))

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The GetTabModuleSettings Method returns a hashtable of
        ''' custom module/tab specific settings from the database.  This method is
        ''' used by some user control modules (Xml, Image, etc) to access misc
        ''' settings.
        ''' </summary>
        ''' <returns>A Hashtable of settings (key/value pairs)</returns>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="moduleSettings">A Hashtable of module Settings</param>
        '''	<param name="tabModuleSettings">A Hashtable of tabModule Settings to</param>
        ''' <history>
        ''' 	[cnurse]	07/08/2006 Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetTabModuleSettings(ByVal moduleSettings As Hashtable, ByVal tabModuleSettings As Hashtable) As Hashtable

            ' add the TabModuleSettings to the ModuleSettings
            For Each strKey As String In tabModuleSettings.Keys
                moduleSettings(strKey) = tabModuleSettings(strKey)
            Next

            'Return the modifed ModuleSettings
            Return moduleSettings

        End Function

        Public Shared Function GetPortalAliasInfo(ByVal PortalAlias As String) As PortalAliasInfo

            Dim strPortalAlias As String

            ' try the specified alias first
            Dim objPortalAliasInfo As PortalAliasInfo = GetPortalAliasLookup(PortalAlias.ToLower)

            ' domain.com and www.domain.com should be synonymous
            If objPortalAliasInfo Is Nothing Then
                If PortalAlias.ToLower.StartsWith("www.") Then
                    ' try alias without the "www." prefix
                    strPortalAlias = PortalAlias.Replace("www.", "")
                Else ' try the alias with the "www." prefix
                    strPortalAlias = String.Concat("www.", PortalAlias)
                End If
                ' perform the lookup
                objPortalAliasInfo = GetPortalAliasLookup(strPortalAlias.ToLower)
            End If

            ' allow domain wildcards 
            If objPortalAliasInfo Is Nothing Then
                ' remove the domain prefix ( ie. anything.domain.com = domain.com )
                If PortalAlias.IndexOf(".") <> -1 Then
                    strPortalAlias = PortalAlias.Substring(PortalAlias.IndexOf(".") + 1)
                Else ' be sure we have a clean string (without leftovers from preceding 'if' block)
                    strPortalAlias = PortalAlias
                End If
                If objPortalAliasInfo Is Nothing Then
                    ' try an explicit lookup using the wildcard entry ( ie. *.domain.com )
                    objPortalAliasInfo = GetPortalAliasLookup("*." & strPortalAlias.ToLower)
                End If
                If objPortalAliasInfo Is Nothing Then
                    ' try a lookup using the raw domain
                    objPortalAliasInfo = GetPortalAliasLookup(strPortalAlias.ToLower)
                End If
                If objPortalAliasInfo Is Nothing Then
                    ' try a lookup using "www." + raw domain
                    objPortalAliasInfo = GetPortalAliasLookup("www." & strPortalAlias.ToLower)
                End If
            End If

            If objPortalAliasInfo Is Nothing Then
                ' check if this is a fresh install ( no alias values in collection )
                Dim objPortalAliasCollection As PortalAliasCollection = GetPortalAliasLookup()
                If Not objPortalAliasCollection.HasKeys OrElse _
                    (objPortalAliasCollection.Count = 1 And objPortalAliasCollection.Contains("_default")) Then
                    ' relate the PortalAlias to the default portal on a fresh database installation
                    DataProvider.Instance().UpdatePortalAlias(PortalAlias.ToLower)

                    'clear the cachekey "GetPortalByAlias" otherwise portalalias "_default" stays in cache after first install
                    DataCache.RemoveCache("GetPortalByAlias")

                    'try again
                    objPortalAliasInfo = GetPortalAliasLookup(PortalAlias.ToLower)
                End If
            End If

            Return objPortalAliasInfo

        End Function

        Public Shared Function GetPortalByID(ByVal PortalId As Integer, ByVal PortalAlias As String) As String

            Dim retValue As String = ""

            ' get the portal alias collection from the cache
            Dim objPortalAliasCollection As PortalAliasCollection = GetPortalAliasLookup()
            Dim strHTTPAlias As String
            Dim bFound As Boolean = False

            'Do a specified PortalAlias check first
            Dim objPortalAliasInfo As PortalAliasInfo = objPortalAliasCollection(PortalAlias.ToLower)
            If Not objPortalAliasInfo Is Nothing Then
                If objPortalAliasInfo.PortalID = PortalId Then
                    ' set the alias
                    retValue = objPortalAliasInfo.HTTPAlias
                    bFound = True
                End If
            End If

            'No match so iterate through the alias keys
            If Not bFound Then
                For Each key As String In objPortalAliasCollection.Keys
                    ' check if the alias key starts with the portal alias value passed in - we use
                    ' StartsWith because child portals are redirected to the parent portal domain name
                    ' eg. child = 'www.domain.com/child' and parent is 'www.domain.com'
                    ' this allows the parent domain name to resolve to the child alias ( the tabid still identifies the child portalid )
                    objPortalAliasInfo = objPortalAliasCollection(key)

                    strHTTPAlias = objPortalAliasInfo.HTTPAlias.ToLower()
                    If strHTTPAlias.StartsWith(PortalAlias.ToLower) = True And objPortalAliasInfo.PortalID = PortalId Then
                        ' set the alias
                        retValue = objPortalAliasInfo.HTTPAlias
                        Exit For
                    End If

                    ' domain.com and www.domain.com should be synonymous
                    If strHTTPAlias.StartsWith("www.") Then
                        ' try alias without the "www." prefix
                        strHTTPAlias = strHTTPAlias.Replace("www.", "")
                    Else ' try the alias with the "www." prefix
                        strHTTPAlias = String.Concat("www.", strHTTPAlias)
                    End If
                    If strHTTPAlias.StartsWith(PortalAlias.ToLower) = True And objPortalAliasInfo.PortalID = PortalId Then
                        ' set the alias
                        retValue = objPortalAliasInfo.HTTPAlias
                        Exit For
                    End If
                Next
            End If

            Return retValue

        End Function

        Public Shared Function GetPortalByTab(ByVal TabID As Integer, ByVal PortalAlias As String) As String

            Dim intPortalId As Integer = -2

            ' get the tab
            Dim objTabs As New TabController
            Dim objTab As TabInfo = objTabs.GetTab(TabID, Null.NullInteger, False)
            If Not objTab Is Nothing Then
                ' ignore deleted tabs
                If Not objTab.IsDeleted Then
                    intPortalId = objTab.PortalID
                End If
            End If

            GetPortalByTab = Nothing

            Select Case intPortalId
                Case -2 ' tab does not exist
                Case -1 ' host tab
                    ' host tabs are not verified to determine if they belong to the portal alias
                    GetPortalByTab = PortalAlias
                Case Else ' portal tab
                    GetPortalByTab = GetPortalByID(intPortalId, PortalAlias)
            End Select

            Return GetPortalByTab

        End Function

        Public Shared Function GetPortalAliasLookup() As PortalAliasCollection

            Dim objPortalAliasCollection As PortalAliasCollection = CType(DataCache.GetCache("GetPortalByAlias"), PortalAliasCollection)

            If objPortalAliasCollection Is Nothing Then
                Dim objPortalAliasController As New PortalAliasController
                objPortalAliasCollection = objPortalAliasController.GetPortalAliases()
                DataCache.SetCache("GetPortalByAlias", objPortalAliasCollection, Nothing, DateTime.MaxValue, TimeSpan.Zero, Caching.CacheItemPriority.NotRemovable, Nothing)
            End If

            Return objPortalAliasCollection
        End Function

        Public Shared Function GetDatabaseVersion() As IDataReader

            Return DataProvider.Instance().GetDatabaseVersion

        End Function

        Public Shared Sub UpdateDatabaseVersion(ByVal Major As Integer, ByVal Minor As Integer, ByVal Build As Integer)

            DataProvider.Instance().UpdateDatabaseVersion(Major, Minor, Build)

        End Sub

        Public Shared Sub UpgradeDatabaseSchema(ByVal Major As Integer, ByVal Minor As Integer, ByVal Build As Integer)

            DataProvider.Instance().UpgradeDatabaseSchema(Major, Minor, Build)

        End Sub

        Public Shared Function FindDatabaseVersion(ByVal Major As Integer, ByVal Minor As Integer, ByVal Build As Integer) As Boolean

            FindDatabaseVersion = False
            Dim dr As IDataReader = DataProvider.Instance().FindDatabaseVersion(Major, Minor, Build)
            If dr.Read Then
                FindDatabaseVersion = True
            End If
            dr.Close()

        End Function

        Public Shared Function GetProviderPath() As String

            Return DataProvider.Instance().GetProviderPath()

        End Function

        Public Shared Function ExecuteScript(ByVal strScript As String) As String

            Return DataProvider.Instance().ExecuteScript(strScript)

        End Function

        Public Shared Function ExecuteScript(ByVal strScript As String, ByVal UseTransactions As Boolean) As String

            Return DataProvider.Instance().ExecuteScript(strScript, UseTransactions)

        End Function

#End Region

#Region "IPropertyAccess Implementation"

          Public Function GetProperty(ByVal strPropertyName As String, ByVal strFormat As String, ByVal formatProvider As System.Globalization.CultureInfo, ByVal AccessingUser As UserInfo, ByVal AccessLevel As Scope, ByRef PropertyNotFound As Boolean) As String Implements IPropertyAccess.GetProperty
            Dim OutputFormat As String = String.Empty
            If strFormat = String.Empty Then OutputFormat = "g"
            Dim lowerPropertyName As String = strPropertyName.ToLower

            'Content locked for NoSettings
            If AccessLevel = Scope.NoSettings Then PropertyNotFound = True : Return PropertyAccess.ContentLocked

            PropertyNotFound = True
            Dim result As String = String.Empty
            Dim PublicProperty As Boolean = True

            Select Case lowerPropertyName
                Case "url"
                    PublicProperty = True : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.PortalAlias.HTTPAlias(), strFormat)
                Case "portalid"
                    PublicProperty = True : PropertyNotFound = False : result = (Me.PortalId.ToString(OutputFormat, formatProvider))
                Case "portalname"
                    PublicProperty = True : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.PortalName, strFormat)
                Case "homedirectory"
                    PublicProperty = True : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.HomeDirectory, strFormat)
                Case "homedirectorymappath"
                    PublicProperty = False : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.HomeDirectoryMapPath, strFormat)
                Case "logofile"
                    PublicProperty = True : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.LogoFile, strFormat)
                Case "footertext"
                    PublicProperty = True : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.FooterText, strFormat)
                Case "expirydate"
                    PublicProperty = False : PropertyNotFound = False : result = (Me.ExpiryDate.ToString(OutputFormat, formatProvider))
                Case "userregistration"
                    PublicProperty = False : PropertyNotFound = False : result = (Me.UserRegistration.ToString(OutputFormat, formatProvider))
                Case "banneradvertising"
                    PublicProperty = False : PropertyNotFound = False : result = (Me.BannerAdvertising.ToString(OutputFormat, formatProvider))
                Case "currency"
                    PublicProperty = True : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.Currency, strFormat)
                Case "administratorid"
                    PublicProperty = False : PropertyNotFound = False : result = (Me.AdministratorId.ToString(OutputFormat, formatProvider))
                Case "email"
                    PublicProperty = True : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.Email, strFormat)
                Case "hostfee"
                    PublicProperty = False : PropertyNotFound = False : result = (Me.HostFee.ToString(OutputFormat, formatProvider))
                Case "hostspace"
                    PublicProperty = False : PropertyNotFound = False : result = (Me.HostSpace.ToString(OutputFormat, formatProvider))
                Case "pagequota"
                    PublicProperty = False : PropertyNotFound = False : result = (Me.PageQuota.ToString(OutputFormat, formatProvider))
                Case "userquota"
                    PublicProperty = False : PropertyNotFound = False : result = (Me.UserQuota.ToString(OutputFormat, formatProvider))
                Case "administratorroleid"
                    PublicProperty = False : PropertyNotFound = False : result = (Me.AdministratorRoleId.ToString(OutputFormat, formatProvider))
                Case "administratorrolename"
                    PublicProperty = False : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.AdministratorRoleName, strFormat)
                Case "registeredroleid"
                    PublicProperty = False : PropertyNotFound = False : result = (Me.RegisteredRoleId.ToString(OutputFormat, formatProvider))
                Case "registeredrolename"
                    PublicProperty = False : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.RegisteredRoleName, strFormat)
                Case "description"
                    PublicProperty = True : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.Description, strFormat)
                Case "keywords"
                    PublicProperty = True : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.KeyWords, strFormat)
                Case "backgroundfile"
                    PublicProperty = True : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.BackgroundFile, strFormat)
                Case "siteloghistory"
                    PublicProperty = False : PropertyNotFound = False : result = (Me.SiteLogHistory.ToString(OutputFormat, formatProvider))
                Case "admintabid"
                    PublicProperty = False : PropertyNotFound = False : result = (Me.AdminTabId.ToString(OutputFormat, formatProvider))
                Case "supertabid"
                    PublicProperty = False : PropertyNotFound = False : result = (Me.SuperTabId.ToString(OutputFormat, formatProvider))
                Case "splashtabid"
                    PublicProperty = False : PropertyNotFound = False : result = (Me.SplashTabId.ToString(OutputFormat, formatProvider))
                Case "hometabid"
                    PublicProperty = False : PropertyNotFound = False : result = (Me.HomeTabId.ToString(OutputFormat, formatProvider))
                Case "logintabid"
                    PublicProperty = False : PropertyNotFound = False : result = (Me.LoginTabId.ToString(OutputFormat, formatProvider))
                Case "usertabid"
                    PublicProperty = False : PropertyNotFound = False : result = (Me.UserTabId.ToString(OutputFormat, formatProvider))
                Case "defaultlanguage"
                    PublicProperty = True : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.DefaultLanguage, strFormat)
                Case "timezoneoffset"
                    PublicProperty = True : PropertyNotFound = False : result = (Me.TimeZoneOffset.ToString(OutputFormat, formatProvider))
                Case "version"
                    PublicProperty = False : PropertyNotFound = False : result = PropertyAccess.FormatString(Me.Version, strFormat)
                Case "users"
                    PublicProperty = False : PropertyNotFound = False : result = (Me.Users.ToString(OutputFormat, formatProvider))
                Case "pages"
                    PublicProperty = False : PropertyNotFound = False : result = (Me.Pages.ToString(OutputFormat, formatProvider))
                Case "contentvisible"
                    PublicProperty = False : PropertyNotFound = False : result = (PropertyAccess.Boolean2LocalizedYesNo(Me.ContentVisible, formatProvider))
                Case "controlpanelvisible"
                    PublicProperty = False : PropertyNotFound = False : result = (PropertyAccess.Boolean2LocalizedYesNo(Me.ControlPanelVisible, formatProvider))
            End Select

            If Not PublicProperty And AccessLevel <> Scope.Debug Then
                PropertyNotFound = True
                result = PropertyAccess.ContentLocked
            End If

            Return result
        End Function

        Public ReadOnly Property Cacheability() As CacheLevel Implements Services.Tokens.IPropertyAccess.Cacheability
            Get
                Return CacheLevel.fullyCacheable
            End Get
        End Property
#End Region

    End Class

End Namespace
