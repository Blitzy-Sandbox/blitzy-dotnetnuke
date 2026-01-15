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
Imports System.IO
Imports System.Xml
Imports System.Web

Imports DotNetNuke.Security.Roles
Imports DotNetNuke.Entities.Modules
Imports DotNetNuke.Entities.Modules.Definitions
Imports DotNetNuke.Entities.Profile
Imports DotNetNuke.Entities.Tabs
Imports DotNetNuke.Entities.Users
Imports DotNetNuke.Entities.Portals
Imports DotNetNuke.Entities.Host
'Imports DotNetNuke.Framework.Providers
Imports DotNetNuke.Security
Imports DotNetNuke.Security.Membership
Imports DotNetNuke.Services.FileSystem
Imports DotNetNuke.Services.Packages
Imports DotNetNuke.Services.Personalization
Imports DotNetNuke.Modules.Admin.ResourceInstaller
Imports DotNetNuke.UI.Skins

Namespace DotNetNuke.Services.Upgrade

    ''' -----------------------------------------------------------------------------
    ''' <summary>
    ''' The Upgrade class provides Shared/Static methods to Upgrade/Install
    '''	a DotNetNuke Application
    ''' </summary>
    ''' <remarks>
    ''' </remarks>
    ''' <history>
    ''' 	[cnurse]	11/6/2004	documented
    ''' </history>
    ''' -----------------------------------------------------------------------------
    Public Class Upgrade

#Region "Private Shared Field"

        Private Shared startTime As DateTime
        Private Shared upgradeMemberShipProvider As Boolean = True

#End Region

#Region "Public Property"

        Public Shared ReadOnly Property DefaultProvider() As String
            Get
                Return Config.GetDefaultProvider("data").Name
            End Get
        End Property

        Public Shared ReadOnly Property RunTime() As TimeSpan
            Get
                Dim currentTime As DateTime = DateTime.Now()
                Return currentTime.Subtract(startTime)
            End Get
        End Property

#End Region

#Region "Private Methods"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' AddAdminPage adds an Admin Tab Page
        ''' </summary>
        '''	<param name="Portal">The Portal</param>
        '''	<param name="TabName">The Name to give this new Tab</param>
        '''	<param name="TabIconFile">The Icon for this new Tab</param>
        '''	<param name="IsVisible">A flag indicating whether the tab is visible</param>
        ''' <history>
        ''' 	[cnurse]	11/11/2004	created 
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Overloads Shared Function AddAdminPage(ByVal Portal As PortalInfo, ByVal TabName As String, ByVal TabIconFile As String, ByVal IsVisible As Boolean) As TabInfo

            Dim objTabController As New TabController
            Dim AdminPage As TabInfo = objTabController.GetTab(Portal.AdminTabId, Portal.PortalID, False)

            If Not AdminPage Is Nothing Then
                Dim objTabPermissions As New Security.Permissions.TabPermissionCollection
                AddPagePermission(objTabPermissions, "View", Convert.ToInt32(Portal.AdministratorRoleId))
                Return AddPage(AdminPage, TabName, TabIconFile, IsVisible, objTabPermissions, True)
            Else
                Return Nothing
            End If

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' AddAdminPages adds an Admin Page and an associated Module to all configured Portals
        ''' </summary>
        '''	<param name="TabName">The Name to give this new Tab</param>
        '''	<param name="TabIconFile">The Icon for this new Tab</param>
        '''	<param name="IsVisible">A flag indicating whether the tab is visible</param>
        '''	<param name="ModuleDefId">The Module Deinition Id for the module to be aded to this tab</param>
        '''	<param name="ModuleTitle">The Module's title</param>
        '''	<param name="ModuleIconFile">The Module's icon</param>
        ''' <history>
        ''' 	[cnurse]	11/16/2004	created 
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Overloads Shared Sub AddAdminPages(ByVal TabName As String, ByVal TabIconFile As String, ByVal IsVisible As Boolean, ByVal ModuleDefId As Integer, ByVal ModuleTitle As String, ByVal ModuleIconFile As String)

            'Call overload with InheritPermisions=True
            AddAdminPages(TabName, TabIconFile, IsVisible, ModuleDefId, ModuleTitle, ModuleIconFile, True)
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' AddAdminPages adds an Admin Page and an associated Module to all configured Portals
        ''' </summary>
        '''	<param name="TabName">The Name to give this new Tab</param>
        '''	<param name="TabIconFile">The Icon for this new Tab</param>
        '''	<param name="IsVisible">A flag indicating whether the tab is visible</param>
        '''	<param name="ModuleDefId">The Module Deinition Id for the module to be aded to this tab</param>
        '''	<param name="ModuleTitle">The Module's title</param>
        '''	<param name="ModuleIconFile">The Module's icon</param>
        '''	<param name="InheritPermissions">Modules Inherit the Pages View Permisions</param>
        ''' <history>
        ''' 	[cnurse]	11/11/2004	created 
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Overloads Shared Sub AddAdminPages(ByVal TabName As String, ByVal TabIconFile As String, ByVal IsVisible As Boolean, ByVal ModuleDefId As Integer, ByVal ModuleTitle As String, ByVal ModuleIconFile As String, ByVal InheritPermissions As Boolean)

            Dim objPortals As New PortalController
            Dim objPortal As PortalInfo
            Dim arrPortals As ArrayList = objPortals.GetPortals
            Dim intPortal As Integer
            Dim newPage As TabInfo

            'Add Page to Admin Menu of all configured Portals
            For intPortal = 0 To arrPortals.Count - 1
                objPortal = CType(arrPortals(intPortal), PortalInfo)

                'Create New Admin Page (or get existing one)
                newPage = AddAdminPage(objPortal, TabName, TabIconFile, IsVisible)

                'Add Module To Page
                AddModuleToPage(newPage, ModuleDefId, ModuleTitle, ModuleIconFile, InheritPermissions)
            Next

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' AddSearchResults adds a top level Hidden Search Results Page
        ''' </summary>
        '''	<param name="ModuleDefId">The Module Deinition Id for the Search Results Module</param>
        ''' <history>
        ''' 	[cnurse]	11/11/2004	created 
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Shared Sub AddSearchResults(ByVal ModuleDefId As Integer)

            Dim objPortals As New PortalController
            Dim objPortal As PortalInfo
            Dim arrPortals As ArrayList = objPortals.GetPortals
            Dim intPortal As Integer
            Dim newPage As TabInfo

            'Add Page to Admin Menu of all configured Portals
            For intPortal = 0 To arrPortals.Count - 1
                Dim objTabPermissions As New Security.Permissions.TabPermissionCollection

                objPortal = CType(arrPortals(intPortal), PortalInfo)

                AddPagePermission(objTabPermissions, "View", Convert.ToInt32(Common.Globals.glbRoleAllUsers))
                AddPagePermission(objTabPermissions, "View", Convert.ToInt32(objPortal.AdministratorRoleId))
                AddPagePermission(objTabPermissions, "Edit", Convert.ToInt32(objPortal.AdministratorRoleId))

                'Create New Page (or get existing one)
                newPage = AddPage(objPortal.PortalID, Null.NullInteger, "Search Results", "", False, objTabPermissions, False)

                'Add Module To Page
                AddModuleToPage(newPage, ModuleDefId, "Search Results", "")
            Next

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' AddHostPage adds a Host Tab Page
        ''' </summary>
        '''	<param name="TabName">The Name to give this new Tab</param>
        '''	<param name="TabIconFile">The Icon for this new Tab</param>
        '''	<param name="IsVisible">A flag indicating whether the tab is visible</param>
        ''' <history>
        ''' 	[cnurse]	11/11/2004	created 
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Overloads Shared Function AddHostPage(ByVal TabName As String, ByVal TabIconFile As String, ByVal IsVisible As Boolean) As TabInfo

            Dim objTabController As New TabController
            Dim HostPage As TabInfo = objTabController.GetTabByName("Host", Null.NullInteger)

            If Not HostPage Is Nothing Then
                Dim objTabPermissions As New Security.Permissions.TabPermissionCollection
                AddPagePermission(objTabPermissions, "View", Convert.ToInt32(Common.Globals.glbRoleSuperUser))
                Return AddPage(HostPage, TabName, TabIconFile, IsVisible, objTabPermissions, True)
            Else
                Return Nothing
            End If

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' AddModuleToPage adds a module to a Page
        ''' </summary>
        ''' <remarks>
        ''' This overload assumes ModulePermissions will be inherited
        ''' </remarks>
        '''	<param name="page">The Page to add the Module to</param>
        '''	<param name="ModuleDefId">The Module Deinition Id for the module to be aded to this tab</param>
        '''	<param name="ModuleTitle">The Module's title</param>
        '''	<param name="ModuleIconFile">The Module's icon</param>
        ''' <history>
        ''' 	[cnurse]	11/11/2004	created 
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Shared Sub AddModuleToPage(ByVal page As TabInfo, ByVal ModuleDefId As Integer, ByVal ModuleTitle As String, ByVal ModuleIconFile As String)

            'Call overload with InheritPermisions=True
            AddModuleToPage(page, ModuleDefId, ModuleTitle, ModuleIconFile, True)
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' AddModuleToPage adds a module to a Page
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="page">The Page to add the Module to</param>
        '''	<param name="ModuleDefId">The Module Deinition Id for the module to be aded to this tab</param>
        '''	<param name="ModuleTitle">The Module's title</param>
        '''	<param name="ModuleIconFile">The Module's icon</param>
        '''	<param name="InheritPermissions">Inherit the Pages View Permisions</param>
        ''' <history>
        ''' 	[cnurse]	11/16/2004	created 
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Shared Sub AddModuleToPage(ByVal page As TabInfo, ByVal ModuleDefId As Integer, ByVal ModuleTitle As String, ByVal ModuleIconFile As String, ByVal InheritPermissions As Boolean)
            Dim objModules As New ModuleController
            Dim objModule As New ModuleInfo
            Dim blnDuplicate As Boolean

            If Not page Is Nothing Then
                blnDuplicate = False
                For Each kvp As KeyValuePair(Of Integer, ModuleInfo) In objModules.GetTabModules(page.TabID)
                    objModule = kvp.Value
                    If objModule.ModuleDefID = ModuleDefId Then
                        blnDuplicate = True
                    End If
                Next

                If Not blnDuplicate Then
                    objModule = New ModuleInfo
                    objModule.ModuleID = Null.NullInteger
                    objModule.PortalID = page.PortalID
                    objModule.TabID = page.TabID
                    objModule.ModuleOrder = -1
                    objModule.ModuleTitle = ModuleTitle
                    objModule.PaneName = glbDefaultPane
                    objModule.ModuleDefID = ModuleDefId
                    objModule.CacheTime = 0
                    objModule.IconFile = ModuleIconFile
                    objModule.AllTabs = False
                    objModule.Visibility = VisibilityState.Maximized
                    objModule.InheritViewPermissions = InheritPermissions

                    Try
                        objModules.AddModule(objModule)
                    Catch
                        ' error adding module
                    End Try
                End If
            End If

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' AddPage adds a Tab Page
        ''' </summary>
        ''' <remarks>
        ''' Adds a Tab to a parentTab
        ''' </remarks>
        '''	<param name="parentTab">The Parent Tab</param>
        '''	<param name="TabName">The Name to give this new Tab</param>
        '''	<param name="TabIconFile">The Icon for this new Tab</param>
        '''	<param name="IsVisible">A flag indicating whether the tab is visible</param>
        '''	<param name="permissions">Page Permissions Collection for this page</param>
        ''' <param name="IsAdmin">Is an admin page</param>
        ''' <history>
        ''' 	[cnurse]	11/11/2004	created 
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Overloads Shared Function AddPage(ByVal parentTab As TabInfo, ByVal TabName As String, ByVal TabIconFile As String, ByVal IsVisible As Boolean, ByVal permissions As Security.Permissions.TabPermissionCollection, ByVal IsAdmin As Boolean) As TabInfo

            Dim ParentId As Integer = Null.NullInteger
            Dim PortalId As Integer = Null.NullInteger

            If Not parentTab Is Nothing Then
                ParentId = parentTab.TabID
                PortalId = parentTab.PortalID
            End If

            Return AddPage(PortalId, ParentId, TabName, TabIconFile, IsVisible, permissions, IsAdmin)

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' AddPage adds a Tab Page
        ''' </summary>
        '''	<param name="PortalId">The Id of the Portal</param>
        '''	<param name="ParentId">The Id of the Parent Tab</param>
        '''	<param name="TabName">The Name to give this new Tab</param>
        '''	<param name="TabIconFile">The Icon for this new Tab</param>
        '''	<param name="IsVisible">A flag indicating whether the tab is visible</param>
        '''	<param name="permissions">Page Permissions Collection for this page</param>
        ''' <param name="IsAdmin">Is and admin page</param>
        ''' <history>
        ''' 	[cnurse]	11/11/2004	created 
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Overloads Shared Function AddPage(ByVal PortalId As Integer, ByVal ParentId As Integer, ByVal TabName As String, ByVal TabIconFile As String, ByVal IsVisible As Boolean, ByVal permissions As Security.Permissions.TabPermissionCollection, ByVal IsAdmin As Boolean) As TabInfo
            Dim objTabs As New TabController
            Dim objTab As TabInfo

            objTab = objTabs.GetTabByName(TabName, PortalId, ParentId)

            If objTab Is Nothing OrElse objTab.ParentId <> ParentId Then
                objTab = New TabInfo
                objTab.TabID = Null.NullInteger
                objTab.PortalID = PortalId
                objTab.TabName = TabName
                objTab.Title = ""
                objTab.Description = ""
                objTab.KeyWords = ""
                objTab.IsVisible = IsVisible
                objTab.DisableLink = False
                objTab.ParentId = ParentId
                objTab.IconFile = TabIconFile
                objTab.AdministratorRoles = Null.NullString
                objTab.IsDeleted = False
                objTab.TabPermissions = permissions
                objTab.TabID = objTabs.AddTab(objTab, Not IsAdmin)
            End If

            Return objTab
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' AddPagePermission adds a TabPermission to a TabPermission Collection
        ''' </summary>
        '''	<param name="permissions">Page Permissions Collection for this page</param>
        '''	<param name="key">The Permission key</param>
        '''	<param name="roleId">The role given the permission</param>
        ''' <history>
        ''' 	[cnurse]	11/11/2004	created 
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Shared Sub AddPagePermission(ByRef permissions As Security.Permissions.TabPermissionCollection, ByVal key As String, ByVal roleId As Integer)


            Dim objPermissionController As New Security.Permissions.PermissionController
            Dim objPermission As Security.Permissions.PermissionInfo = CType(objPermissionController.GetPermissionByCodeAndKey("SYSTEM_TAB", key)(0), Security.Permissions.PermissionInfo)

            Dim objTabPermission As New Security.Permissions.TabPermissionInfo
            objTabPermission.PermissionID = objPermission.PermissionID
            objTabPermission.RoleID = roleId
            objTabPermission.AllowAccess = True
            permissions.Add(objTabPermission)

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' AddModuleDefinition adds a new Core Module Definition to the system
        ''' </summary>
        ''' <remarks>
        '''	This overload asumes the module is an Admin module and not a Premium Module
        ''' </remarks>
        '''	<param name="DesktopModuleName">The Friendly Name of the Module to Add</param>
        '''	<param name="Description">Description of the Module</param>
        '''	<param name="ModuleDefinitionName">The Module Definition Name</param>
        '''	<returns>The Module Definition Id of the new Module</returns>
        ''' <history>
        ''' 	[cnurse]	10/14/2004	documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Overloads Shared Function AddModuleDefinition(ByVal DesktopModuleName As String, ByVal Description As String, ByVal ModuleDefinitionName As String) As Integer
            'Call overload with Premium=False and Admin=True
            Return AddModuleDefinition(DesktopModuleName, Description, ModuleDefinitionName, False, True, Null.NullString, Null.NullString)

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' AddModuleDefinition adds a new Core Module Definition to the system
        ''' </summary>
        ''' <remarks>
        '''	This overload allows the caller to determine whether the module is an Admin module 
        '''	or a Premium Module
        ''' </remarks>
        '''	<param name="DesktopModuleName">The Friendly Name of the Module to Add</param>
        '''	<param name="Description">Description of the Module</param>
        '''	<param name="ModuleDefinitionName">The Module Definition Name</param>
        '''	<param name="Premium">A flag representing whether the module is a Premium module</param>
        '''	<param name="Admin">A flag representing whether the module is an Admin module</param>
        '''	<returns>The Module Definition Id of the new Module</returns>
        ''' <history>
        ''' 	[cnurse]	10/14/2004	documented
        '''     [cnurse]    11/11/2004  removed addition of Module Control (now in AddMOduleControl)
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Overloads Shared Function AddModuleDefinition(ByVal DesktopModuleName As String, ByVal Description As String, ByVal ModuleDefinitionName As String, ByVal Premium As Boolean, ByVal Admin As Boolean) As Integer

            'Call overload with Controller=NulString and HelpUrl=NullString
            Return AddModuleDefinition(DesktopModuleName, Description, ModuleDefinitionName, Premium, Admin, Null.NullString, Null.NullString)

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' AddModuleDefinition adds a new Core Module Definition to the system
        ''' </summary>
        ''' <remarks>
        '''	This overload allows the caller to determine whether the module has a controller
        ''' class
        ''' </remarks>
        '''	<param name="DesktopModuleName">The Friendly Name of the Module to Add</param>
        '''	<param name="Description">Description of the Module</param>
        '''	<param name="ModuleDefinitionName">The Module Definition Name</param>
        '''	<param name="Premium">A flag representing whether the module is a Premium module</param>
        '''	<param name="Admin">A flag representing whether the module is an Admin module</param>
        '''	<param name="Controller">The Controller Class string</param>
        '''	<returns>The Module Definition Id of the new Module</returns>
        ''' <history>
        ''' 	[cnurse]	10/14/2004	documented
        '''     [cnurse]    11/11/2004  removed addition of Module Control (now in AddMOduleControl)
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Overloads Shared Function AddModuleDefinition(ByVal DesktopModuleName As String, ByVal Description As String, ByVal ModuleDefinitionName As String, ByVal Premium As Boolean, ByVal Admin As Boolean, ByVal Controller As String) As Integer

            Return AddModuleDefinition(DesktopModuleName, Description, ModuleDefinitionName, Premium, Admin, Controller, Null.NullString)

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' AddModuleDefinition adds a new Core Module Definition to the system
        ''' </summary>
        ''' <remarks>
        '''	This overload allows the caller to determine whether the module has a controller
        ''' class
        ''' </remarks>
        '''	<param name="DesktopModuleName">The Friendly Name of the Module to Add</param>
        '''	<param name="Description">Description of the Module</param>
        '''	<param name="ModuleDefinitionName">The Module Definition Name</param>
        '''	<param name="Premium">A flag representing whether the module is a Premium module</param>
        '''	<param name="Admin">A flag representing whether the module is an Admin module</param>
        '''	<param name="Controller">The Controller Class string</param>
        '''	<param name="Version">The Module Version</param>
        '''	<returns>The Module Definition Id of the new Module</returns>
        ''' <history>
        ''' 	[cnurse]	10/14/2004	documented
        '''     [cnurse]    11/11/2004  removed addition of Module Control (now in AddMOduleControl)
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Overloads Shared Function AddModuleDefinition(ByVal DesktopModuleName As String, ByVal Description As String, ByVal ModuleDefinitionName As String, ByVal Premium As Boolean, ByVal Admin As Boolean, ByVal Controller As String, ByVal Version As String) As Integer
            Dim objDesktopModules As New DesktopModuleController

            ' check if desktop module exists
            Dim objDesktopModule As DesktopModuleInfo = objDesktopModules.GetDesktopModuleByModuleName(DesktopModuleName)
            If objDesktopModule Is Nothing Then
                objDesktopModule = New DesktopModuleInfo

                objDesktopModule.DesktopModuleID = Null.NullInteger
                objDesktopModule.FriendlyName = DesktopModuleName
                objDesktopModule.FolderName = DesktopModuleName
                objDesktopModule.ModuleName = DesktopModuleName
                objDesktopModule.Description = Description
                objDesktopModule.Version = Version
                objDesktopModule.IsPremium = Premium
                objDesktopModule.IsAdmin = Admin
                objDesktopModule.BusinessControllerClass = Controller

                objDesktopModule.DesktopModuleID = objDesktopModules.AddDesktopModule(objDesktopModule)
            End If

            Dim objModuleDefinitions As New ModuleDefinitionController

            ' check if module definition exists
            Dim objModuleDefinition As ModuleDefinitionInfo = objModuleDefinitions.GetModuleDefinitionByName(objDesktopModule.DesktopModuleID, ModuleDefinitionName)
            If objModuleDefinition Is Nothing Then
                objModuleDefinition = New ModuleDefinitionInfo

                objModuleDefinition.ModuleDefID = Null.NullInteger
                objModuleDefinition.DesktopModuleID = objDesktopModule.DesktopModuleID
                objModuleDefinition.FriendlyName = ModuleDefinitionName

                objModuleDefinition.ModuleDefID = objModuleDefinitions.AddModuleDefinition(objModuleDefinition)
            End If

            Return objModuleDefinition.ModuleDefID

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' AddModuleControl adds a new Module Control to the system
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="ModuleDefId">The Module Definition Id</param>
        '''	<param name="ControlKey">The key for this control in the Definition</param>
        '''	<param name="ControlTitle">The title of this control</param>
        '''	<param name="ControlSrc">Te source of ths control</param>
        '''	<param name="IconFile">The icon file</param>
        '''	<param name="ControlType">The type of control</param>
        '''	<param name="ViewOrder">The vieworder for this module</param>
        ''' <history>
        ''' 	[cnurse]	11/08/2004	documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Overloads Shared Sub AddModuleControl(ByVal ModuleDefId As Integer, ByVal ControlKey As String, ByVal ControlTitle As String, ByVal ControlSrc As String, ByVal IconFile As String, ByVal ControlType As SecurityAccessLevel, ByVal ViewOrder As Integer)

            'Call Overload with HelpUrl = Null.NullString
            AddModuleControl(ModuleDefId, ControlKey, ControlTitle, ControlSrc, IconFile, ControlType, ViewOrder, Null.NullString)
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' AddModuleControl adds a new Module Control to the system
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="ModuleDefId">The Module Definition Id</param>
        '''	<param name="ControlKey">The key for this control in the Definition</param>
        '''	<param name="ControlTitle">The title of this control</param>
        '''	<param name="ControlSrc">Te source of ths control</param>
        '''	<param name="IconFile">The icon file</param>
        '''	<param name="ControlType">The type of control</param>
        '''	<param name="ViewOrder">The vieworder for this module</param>
        '''	<param name="HelpURL">The Help Url</param>
        ''' <history>
        ''' 	[cnurse]	11/08/2004	documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Overloads Shared Sub AddModuleControl(ByVal ModuleDefId As Integer, ByVal ControlKey As String, ByVal ControlTitle As String, ByVal ControlSrc As String, ByVal IconFile As String, ByVal ControlType As SecurityAccessLevel, ByVal ViewOrder As Integer, ByVal HelpURL As String)

            ' check if module control exists
            Dim objModuleControl As ModuleControlInfo = ModuleControlController.GetModuleControlByKeyAndSrc(ModuleDefId, ControlKey, ControlSrc)
            If objModuleControl Is Nothing Then
                objModuleControl = New ModuleControlInfo

                objModuleControl.ModuleControlID = Null.NullInteger
                objModuleControl.ModuleDefID = ModuleDefId
                objModuleControl.ControlKey = ControlKey
                objModuleControl.ControlTitle = ControlTitle
                objModuleControl.ControlSrc = ControlSrc
                objModuleControl.ControlType = ControlType
                objModuleControl.ViewOrder = ViewOrder
                objModuleControl.IconFile = IconFile
                objModuleControl.HelpURL = HelpURL

                ModuleControlController.AddModuleControl(objModuleControl)
            End If
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' CoreModuleExists determines whether a Core Module exists on the system
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="DesktopModuleName">The Friendly Name of the Module</param>
        '''	<returns>True if the Module exists, otherwise False</returns>
        ''' <history>
        ''' 	[cnurse]	10/14/2004	documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Shared Function CoreModuleExists(ByVal DesktopModuleName As String) As Boolean

            Dim blnExists As Boolean = False

            Dim objDesktopModules As New DesktopModuleController

            Dim objDesktopModule As DesktopModuleInfo = objDesktopModules.GetDesktopModuleByModuleName(DesktopModuleName)
            If Not objDesktopModule Is Nothing Then
                blnExists = True
            Else
                blnExists = False
            End If

            Return blnExists

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' DeleteFiles - clean up deprecated files and folders
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <param name="strVersion">The Version being Upgraded</param>
        ''' <history>
        ''' 	[swalker]	11/09/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Shared Function DeleteFiles(ByVal strVersion As String) As String
            Dim strExceptions As String = ""

            Try
                Dim strListFile As String = DotNetNuke.Common.HostMapPath & strVersion & ".txt"

                If File.Exists(strListFile) Then
                    ' read list file
                    Dim objStreamReader As StreamReader
                    objStreamReader = File.OpenText(strListFile)
                    Dim arrPaths As Array = objStreamReader.ReadToEnd.Split(ControlChars.CrLf.ToCharArray())
                    objStreamReader.Close()
                    strExceptions = FileSystemUtils.DeleteFiles(arrPaths)
                End If
            Catch ex As Exception
                strExceptions += "Error: " & ex.Message & vbCrLf
            End Try

            Return strExceptions
        End Function

        ''' <summary>
        ''' DeleteFiles - clean up deprecated files and folders
        ''' </summary>
        ''' <param name="strVersion">The Version being Upgraded</param>
        ''' <returns></returns>
        ''' -----------------------------------------------------------------------------
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' [swalker]	11/09/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Shared Function UpdateConfig(ByVal strVersion As String) As String
            Dim strExceptions As String = ""

            Dim strConfigFile As String = DotNetNuke.Common.HostMapPath & strVersion & ".config"

            If File.Exists(strConfigFile) Then
                FileSystemUtils.SetFileAttributes(strConfigFile, FileAttributes.Normal)

                'Create XmlMerge instance from config file source
                Using stream As StreamReader = File.OpenText(strConfigFile)
                    Try
                        Dim merge As XmlMerge = New XmlMerge(stream, strVersion, "Core Upgrade")

                        'Process merge
                        merge.UpdateConfigs()

                    Catch ex As Exception
                        strExceptions += "Error: " & ex.Message & vbCrLf
                    End Try
                End Using
            End If

            Return strExceptions
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' ExecuteScript executes a SQl script file
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="strScriptFile">The script to Execute</param>
        '''	<param name="version">The script version</param>
        ''' <history>
        ''' 	[cnurse]	11/09/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Shared Function ExecuteScript(ByVal strScriptFile As String, ByVal version As String, ByVal writeFeedback As Boolean) As String

            If writeFeedback Then
                HtmlUtils.WriteFeedback(HttpContext.Current.Response, 2, "Executing Script: " + Path.GetFileName(strScriptFile))
            End If

            Dim strExceptions As String

            ' read script file for installation
            Dim objStreamReader As StreamReader
            objStreamReader = File.OpenText(strScriptFile)
            Dim strScript As String = objStreamReader.ReadToEnd
            objStreamReader.Close()

            ' execute SQL installation script
            strExceptions = PortalSettings.ExecuteScript(strScript)

            '' perform version specific application upgrades
            If version <> "" Then
                strExceptions += UpgradeApplication(version, writeFeedback)

                ' delete files which are no longer used
                strExceptions += DeleteFiles(version)

                'execute config file updates
                strExceptions += UpdateConfig(version)
            End If

            ' log the results
            Try
                Dim objStream As StreamWriter
                objStream = File.CreateText(strScriptFile.Replace("." & DefaultProvider, "") & ".log")
                objStream.WriteLine(strExceptions)
                objStream.Close()
            Catch
                ' does not have permission to create the log file
            End Try

            If writeFeedback Then
                HtmlUtils.WriteScriptSuccessError(HttpContext.Current.Response, (strExceptions = ""), Path.GetFileName(strScriptFile).Replace("." & DefaultProvider, ".log"))
            End If

            Return strExceptions

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetModuleDefinition gets the Module Definition Id of a module
        ''' </summary>
        '''	<param name="DesktopModuleName">The Friendly Name of the Module to Add</param>
        '''	<param name="ModuleDefinitionName">The Module Definition Name</param>
        '''	<returns>The Module Definition Id of the Module (-1 if no module definition)</returns>
        ''' <history>
        ''' 	[cnurse]	11/16/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Shared Function GetModuleDefinition(ByVal DesktopModuleName As String, ByVal ModuleDefinitionName As String) As Integer
            Dim objDesktopModules As New DesktopModuleController

            ' get desktop module 
            Dim objDesktopModule As DesktopModuleInfo = objDesktopModules.GetDesktopModuleByModuleName(DesktopModuleName)
            If objDesktopModule Is Nothing Then
                Return -1
            End If

            Dim objModuleDefinitions As New ModuleDefinitionController

            ' get module definition 
            Dim objModuleDefinition As ModuleDefinitionInfo = objModuleDefinitions.GetModuleDefinitionByName(objDesktopModule.DesktopModuleID, ModuleDefinitionName)
            If objModuleDefinition Is Nothing Then
                Return -1
            End If

            Return objModuleDefinition.ModuleDefID

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' HostTabExists determines whether a tab of a given name exists under the Host tab
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="TabName">The Name of the Tab</param>
        '''	<returns>True if the Tab exists, otherwise False</returns>
        ''' <history>
        ''' 	[cnurse]	11/08/2004	documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Shared Function HostTabExists(ByVal TabName As String) As Boolean

            Dim blnExists As Boolean = False

            Dim objTabController As New TabController

            Dim objTabInfo As TabInfo = objTabController.GetTabByName(TabName, Null.NullInteger)
            If Not objTabInfo Is Nothing Then
                blnExists = True
            Else
                blnExists = False
            End If

            Return blnExists

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' InstallMemberRoleProvider - Installs the MemberRole Provider Db objects
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="strProviderPath">The Path to the Provider Directory</param>
        ''' <history>
        ''' 	[cnurse]	02/02/2005	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Shared Function InstallMemberRoleProvider(ByVal strProviderPath As String, ByVal writeFeedback As Boolean) As String
            Dim strExceptions As String = ""

            Dim installMemberRole As Boolean = True
            If Not Config.GetSetting("InstallMemberRole") Is Nothing Then
                installMemberRole = Boolean.Parse(Config.GetSetting("InstallMemberRole"))
            End If

            If installMemberRole Then
                If writeFeedback Then
                    HtmlUtils.WriteFeedback(HttpContext.Current.Response, 0, "Installing MemberRole Provider:<br>")
                End If

                'Install Common
                strExceptions += InstallMemberRoleProviderScript(strProviderPath, "InstallCommon", writeFeedback)
                'Install Membership
                strExceptions += InstallMemberRoleProviderScript(strProviderPath, "InstallMembership", writeFeedback)
                'Install Profile
                strExceptions += InstallMemberRoleProviderScript(strProviderPath, "InstallProfile", writeFeedback)
                'Install Roles
                strExceptions += InstallMemberRoleProviderScript(strProviderPath, "InstallRoles", writeFeedback)

                'As we have just done an Install set the Upgrade Flag to false
                upgradeMemberShipProvider = False
            End If

            Return strExceptions
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' InstallMemberRoleProviderScript - Installs a specific MemberRole Provider script
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="providerPath">The Path to the Provider Directory</param>
        '''	<param name="scriptFile">The Name of the Script File</param>
        '''	<param name="writeFeedback">Whether or not to echo results</param>
        ''' <history>
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Shared Function InstallMemberRoleProviderScript(ByVal providerPath As String, ByVal scriptFile As String, ByVal writeFeedback As Boolean) As String
            Dim objStreamReader As StreamReader
            Dim strExceptions As String = ""

            If writeFeedback Then
                HtmlUtils.WriteFeedback(HttpContext.Current.Response, 2, "Executing Script: " & scriptFile & "<br>")
            End If

            Dim strScript As String = ""
            objStreamReader = File.OpenText(providerPath + scriptFile & ".sql")
            strScript = objStreamReader.ReadToEnd
            objStreamReader.Close()
            strExceptions = PortalSettings.ExecuteScript(strScript)

            ' log the results
            Try
                Dim objStream As StreamWriter
                objStream = File.CreateText(providerPath + scriptFile & ".log")
                objStream.WriteLine(strExceptions)
                objStream.Close()
            Catch
                ' does not have permission to create the log file
            End Try

            Return strExceptions
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' ParseDesktopModules parses the Host Template's Desktop Modules node
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="node">The Desktop Modules node</param>
        ''' <history>
        ''' 	[cnurse]	11/08/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Shared Sub ParseDesktopModules(ByVal node As XmlNode)
            'TODO - This method is obsolete since the modules are now seperated from the core
            Dim desktopModuleNode As XmlNode
            Dim moduleNode As XmlNode
            Dim controlNode As XmlNode
            Dim ModuleDefID As Integer
            Dim controlType As SecurityAccessLevel

            ' Parse the desktopmodule nodes
            For Each desktopModuleNode In node.SelectNodes("desktopmodule")
                Dim strDescription As String = XmlUtils.GetNodeValue(desktopModuleNode, "description")
                Dim strVersion As String = XmlUtils.GetNodeValue(desktopModuleNode, "version")
                Dim strControllerClass As String = XmlUtils.GetNodeValue(desktopModuleNode, "businesscontrollerclass")

                ' Parse the module nodes
                For Each moduleNode In desktopModuleNode.SelectNodes("modules/module")
                    Dim strName As String = XmlUtils.GetNodeValue(moduleNode, "friendlyname")

                    HtmlUtils.WriteFeedback(HttpContext.Current.Response, 2, "Installing " & strName & ":<br>")
                    ModuleDefID = AddModuleDefinition(strName, strDescription, strName, False, False, strControllerClass, strVersion)

                    ' Parse the control nodes
                    For Each controlNode In moduleNode.SelectNodes("controls/control")
                        Dim strKey As String = XmlUtils.GetNodeValue(controlNode, "key")
                        Dim strTitle As String = XmlUtils.GetNodeValue(controlNode, "title")
                        Dim strSrc As String = XmlUtils.GetNodeValue(controlNode, "src")
                        Dim strIcon As String = XmlUtils.GetNodeValue(controlNode, "iconfile")
                        Dim strType As String = XmlUtils.GetNodeValue(controlNode, "type")
                        Select Case XmlUtils.GetNodeValue(controlNode, "type")
                            Case "View"
                                controlType = SecurityAccessLevel.View
                            Case "Edit"
                                controlType = SecurityAccessLevel.Edit
                        End Select
                        Dim strHelpUrl As String = XmlUtils.GetNodeValue(controlNode, "helpurl")

                        'Add Control to System
                        AddModuleControl(ModuleDefID, strKey, strTitle, strSrc, strIcon, controlType, 0, strHelpUrl)
                    Next
                Next
            Next desktopModuleNode


        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' ParseFiles parses the Host Template's Files node
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="node">The Files node</param>
        '''	<param name="portalId">The PortalId (-1 for Host Files)</param>
        ''' <history>
        ''' 	[cnurse]	11/08/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Shared Sub ParseFiles(ByVal node As XmlNode, ByVal portalId As Integer)

            Dim fileNode As XmlNode
            Dim objController As New DotNetNuke.Services.FileSystem.FileController

            'Parse the File nodes
            For Each fileNode In node.SelectNodes("file")
                Dim strFileName As String = XmlUtils.GetNodeValue(fileNode, "filename")
                Dim strExtenstion As String = XmlUtils.GetNodeValue(fileNode, "extension")
                Dim fileSize As Long = Long.Parse(XmlUtils.GetNodeValue(fileNode, "size"))
                Dim iWidth As Integer = XmlUtils.GetNodeValueInt(fileNode, "width")
                Dim iHeight As Integer = XmlUtils.GetNodeValueInt(fileNode, "height")
                Dim strType As String = XmlUtils.GetNodeValue(fileNode, "contentType")
                Dim strFolder As String = XmlUtils.GetNodeValue(fileNode, "folder")

                Dim objFolders As New FolderController
                Dim objFolder As FolderInfo = objFolders.GetFolder(portalId, strFolder, False)
                objController.AddFile(portalId, strFileName, strExtenstion, fileSize, iWidth, iHeight, strType, strFolder, objFolder.FolderID, True)
            Next

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' RemoveCoreModule removes a Core Module from the system
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="DesktopModuleName">The Friendly Name of the Module to Remove</param>
        '''	<param name="ParentTabName">The Name of the parent Tab/Page for this module</param>
        '''	<param name="TabName">The Name to tab that contains the Module</param>
        '''	<param name="TabRemove">A flag to determine whether to remove the Tab if it has no
        '''	other modules</param>
        ''' <history>
        ''' 	[cnurse]	10/14/2004	documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Shared Sub RemoveCoreModule(ByVal DesktopModuleName As String, ByVal ParentTabName As String, ByVal TabName As String, ByVal TabRemove As Boolean)

            Dim objTabs As New TabController
            Dim objTab As TabInfo
            Dim objModules As New ModuleController
            Dim objModule As New ModuleInfo
            Dim ParentId As Integer
            Dim intIndex As Integer
            Dim intModuleDefId As Integer
            Dim intDesktopModuleId As Integer

            'Find and remove the Module from the Tab
            Select Case ParentTabName
                Case "Host"
                    'TODO - when we have a need to remove a Host Module
                Case "Admin"
                    Dim objPortals As New PortalController
                    Dim objPortal As PortalInfo

                    Dim arrPortals As ArrayList = objPortals.GetPortals
                    Dim intPortal As Integer

                    'Iterate through the Portals to remove the Module from the Tab
                    For intPortal = 0 To arrPortals.Count - 1
                        objPortal = CType(arrPortals(intPortal), PortalInfo)

                        ParentId = objPortal.AdminTabId
                        objTab = objTabs.GetTabByName(TabName, objPortal.PortalID, ParentId)
                        Dim intCount As Integer = 0

                        'Get the Modules on the Tab
                        For Each kvp As KeyValuePair(Of Integer, ModuleInfo) In objModules.GetTabModules(objTab.TabID)
                            objModule = kvp.Value
                            If objModule.FriendlyName = DesktopModuleName Then
                                'Delete the Module from the Modules list
                                objModules.DeleteModule(objModule.ModuleID)
                                intModuleDefId = objModule.ModuleDefID
                            Else
                                intCount += 1
                            End If
                        Next

                        'If Tab has no modules optionally remove tab
                        If intCount = 0 And TabRemove Then
                            objTabs.DeleteTab(objTab.TabID, objTab.PortalID)
                        End If
                    Next intPortal
            End Select

            'Delete all the Module Controls for this Definition
            Dim arrModuleControls As ArrayList = ModuleControlController.GetModuleControls(intModuleDefId)
            Dim objModuleControl As ModuleControlInfo
            For intIndex = 0 To arrModuleControls.Count - 1
                objModuleControl = DirectCast(arrModuleControls(intIndex), ModuleControlInfo)
                ModuleControlController.DeleteModuleControl(objModuleControl.ModuleControlID)
            Next

            'Get the Module Definition
            Dim objModuleDefinitions As New ModuleDefinitionController
            Dim objModuleDefinition As ModuleDefinitionInfo
            objModuleDefinition = objModuleDefinitions.GetModuleDefinition(intModuleDefId)
            intDesktopModuleId = objModuleDefinition.DesktopModuleID

            'Delete the Module Definition
            objModuleDefinitions.DeleteModuleDefinition(intModuleDefId)

            'Delete the Desktop Module Control
            Dim objDesktopModules As New DesktopModuleController
            objDesktopModules.DeleteDesktopModule(intDesktopModuleId)

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' UpgradeApplication - This overload is used for version specific application upgrade operations. 
        ''' </summary>
        ''' <remarks>
        '''	This should be used for file system modifications or upgrade operations which 
        '''	should only happen once. Database references are not recommended because future 
        '''	versions of the application may result in code incompatibilties.
        ''' </remarks>
        '''	<param name="Version">The Version being Upgraded</param>
        ''' <history>
        ''' 	[cnurse]	11/6/2004	documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Shared Function UpgradeApplication(ByVal Version As String, ByVal writeFeedback As Boolean) As String

            Dim strExceptions As String = ""

            Try

                Select Case Version
                    Case "02.00.00"
                        Dim dr As IDataReader

                        ' change portal upload directory from GUID to ID - this only executes for version 2.0.0
                        Dim strServerPath As String = HttpContext.Current.Request.MapPath(Common.Globals.ApplicationPath)
                        Dim strPortalsDirMapPath As String = ApplicationMapPath + "/Portals/"

                        dr = DataProvider.Instance().GetPortals()
                        While dr.Read
                            ' if GUID folder exists
                            If Directory.Exists(strPortalsDirMapPath & dr("GUID").ToString) = True Then
                                ' if ID folder exists ( this may happen because the 2.x release contains a default ID=0 folder )
                                If Directory.Exists(strPortalsDirMapPath & dr("PortalID").ToString) = True Then
                                    ' rename the ID folder
                                    Try
                                        Directory.Move(strPortalsDirMapPath & dr("PortalID").ToString, strServerPath & "\Portals\" & dr("PortalID").ToString & "_old")
                                    Catch ex As Exception
                                        ' error moving the directory - security issue?
                                        strExceptions += "Could Not Move Folder " & strPortalsDirMapPath & dr("GUID").ToString & " To " & strPortalsDirMapPath & dr("PortalID").ToString & ". Error: " & ex.Message & vbCrLf
                                    End Try
                                End If

                                ' move GUID folder to ID folder
                                Try
                                    Directory.Move(strPortalsDirMapPath & dr("GUID").ToString, strPortalsDirMapPath & dr("PortalID").ToString)
                                Catch ex As Exception
                                    ' error moving the directory - security issue?
                                    strExceptions += "Could Not Move Folder " & strPortalsDirMapPath & dr("GUID").ToString & " To " & strPortalsDirMapPath & dr("PortalID").ToString & ". Error: " & ex.Message & vbCrLf
                                End Try
                            End If
                        End While
                        dr.Close()

                        ' copy the default style sheet to the default portal ( if it does not already exist )
                        If File.Exists(strPortalsDirMapPath + "0\portal.css") = False Then
                            If File.Exists(HostMapPath + "portal.css") Then
                                File.Copy(HostMapPath + "portal.css", strPortalsDirMapPath + "0\portal.css")
                            End If
                        End If

                    Case "02.02.00"
                        Dim strProviderPath As String = PortalSettings.GetProviderPath()
                        If strProviderPath.StartsWith("ERROR:") Then
                            strExceptions += strProviderPath
                            Exit Select
                        End If

                        'Optionally Install the memberRoleProvider
                        strExceptions += InstallMemberRoleProvider(strProviderPath, writeFeedback)

                        Dim objPortalController As New PortalController
                        Dim arrPortals As ArrayList
                        arrPortals = objPortalController.GetPortals()

                        Dim intViewModulePermissionID As Integer
                        Dim intEditModulePermissionID As Integer

                        Dim intViewTabPermissionID As Integer
                        Dim intEditTabPermissionID As Integer

                        Dim intReadFolderPermissionID As Integer
                        Dim intWriteFolderPermissionID As Integer

                        Dim objPermissionController As New Security.Permissions.PermissionController
                        Dim objPermission As New Security.Permissions.PermissionInfo
                        objPermission.PermissionCode = "SYSTEM_MODULE_DEFINITION"
                        objPermission.PermissionKey = "VIEW"
                        objPermission.PermissionName = "View"
                        objPermission.ModuleDefID = Null.NullInteger
                        intViewModulePermissionID = objPermissionController.AddPermission(objPermission)

                        objPermission.PermissionKey = "EDIT"
                        objPermission.PermissionName = "Edit"
                        intEditModulePermissionID = objPermissionController.AddPermission(objPermission)

                        objPermission.PermissionCode = "SYSTEM_TAB"
                        objPermission.PermissionKey = "VIEW"
                        objPermission.PermissionName = "View Tab"
                        intViewTabPermissionID = objPermissionController.AddPermission(objPermission)

                        objPermission.PermissionKey = "EDIT"
                        objPermission.PermissionName = "Edit Tab"
                        intEditTabPermissionID = objPermissionController.AddPermission(objPermission)

                        objPermission.PermissionCode = "SYSTEM_FOLDER"
                        objPermission.PermissionKey = "READ"
                        objPermission.PermissionName = "View Folder"
                        intReadFolderPermissionID = objPermissionController.AddPermission(objPermission)

                        objPermission.PermissionKey = "WRITE"
                        objPermission.PermissionName = "Write to Folder"
                        intWriteFolderPermissionID = objPermissionController.AddPermission(objPermission)


                        Dim objFolderController As New Services.FileSystem.FolderController

                        Dim objFolderPermissionController As New Security.Permissions.FolderPermissionController
                        Dim PortalCount As Integer
                        For PortalCount = 0 To arrPortals.Count - 1
                            Dim objPortal As PortalInfo = CType(arrPortals(PortalCount), PortalInfo)
                            Dim FolderID As Integer = objFolderController.AddFolder(objPortal.PortalID, "", FileSystem.FolderController.StorageLocationTypes.InsecureFileSystem, True, False)

                            Dim objFolderPermission As New Security.Permissions.FolderPermissionInfo
                            objFolderPermission.FolderID = FolderID
                            objFolderPermission.PermissionID = intReadFolderPermissionID
                            objFolderPermission.AllowAccess = True
                            objFolderPermission.RoleID = objPortal.AdministratorRoleId
                            objFolderPermissionController.AddFolderPermission(objFolderPermission)

                            objFolderPermission.PermissionID = intWriteFolderPermissionID
                            objFolderPermissionController.AddFolderPermission(objFolderPermission)

                            'TODO: loop through folders recursively here
                            'in case they created any nested folders
                            'and assign priveledges accordingly
                        Next


                        'Transfer Users to the Membership Provider
                        Dim provider As MembershipProvider = MembershipProvider.Instance()
                        provider.TransferUsersToMembershipProvider()

                        Dim arrModuleDefinitions As ArrayList
                        Dim objModuleDefinitionController As New ModuleDefinitionController
                        arrModuleDefinitions = objModuleDefinitionController.GetModuleDefinitions(Null.NullInteger)

                        Dim arrModules As ArrayList
                        Dim objModuleController As New ModuleController
                        arrModules = objModuleController.GetAllModules()

                        Dim objModulePermissionController As New Security.Permissions.ModulePermissionController
                        Dim ModCount As Integer
                        For ModCount = 0 To arrModules.Count - 1
                            Dim objModule As ModuleInfo = CType(arrModules(ModCount), ModuleInfo)
                            Dim objModulePermission As New Security.Permissions.ModulePermissionInfo
                            objModulePermission.ModuleID = objModule.ModuleID
                            Dim k As Integer
                            Dim roles As String()
                            If objModule.AuthorizedViewRoles.IndexOf(";") > 0 Then
                                roles = Split(objModule.AuthorizedViewRoles, ";")
                                For k = 0 To roles.Length - 1
                                    If IsNumeric(roles(k)) Then
                                        objModulePermission.PermissionID = intViewModulePermissionID
                                        objModulePermission.AllowAccess = True
                                        objModulePermission.RoleID = Convert.ToInt32(roles(k))
                                        objModulePermissionController.AddModulePermission(objModulePermission, objModule.TabID)
                                    End If
                                Next
                            End If
                            If objModule.AuthorizedEditRoles.IndexOf(";") > 0 Then
                                roles = Split(objModule.AuthorizedEditRoles, ";")
                                For k = 0 To roles.Length - 1
                                    If IsNumeric(roles(k)) Then
                                        objModulePermission.PermissionID = intEditModulePermissionID
                                        objModulePermission.AllowAccess = True
                                        objModulePermission.RoleID = Convert.ToInt32(roles(k))
                                        objModulePermissionController.AddModulePermission(objModulePermission, objModule.TabID)
                                    End If
                                Next
                            End If
                        Next

                        Dim arrTabs As ArrayList
                        Dim objTabController As New TabController
                        arrTabs = objTabController.GetAllTabs

                        Dim objTabPermissionController As New Security.Permissions.TabPermissionController
                        For ModCount = 0 To arrTabs.Count - 1
                            Dim objTab As TabInfo = CType(arrTabs(ModCount), TabInfo)
                            Dim objTabPermission As New Security.Permissions.TabPermissionInfo
                            objTabPermission.TabID = objTab.TabID
                            Dim k As Integer
                            Dim roles As String()
                            If objTab.AuthorizedRoles.IndexOf(";") > 0 Then
                                roles = Split(objTab.AuthorizedRoles, ";")
                                For k = 0 To roles.Length - 1
                                    If IsNumeric(roles(k)) Then
                                        objTabPermission.PermissionID = intViewTabPermissionID
                                        objTabPermission.AllowAccess = True
                                        objTabPermission.RoleID = Convert.ToInt32(roles(k))
                                        objTabPermissionController.AddTabPermission(objTabPermission)
                                    End If
                                Next
                            End If
                            If objTab.AdministratorRoles.IndexOf(";") > 0 Then
                                roles = Split(objTab.AdministratorRoles, ";")
                                For k = 0 To roles.Length - 1
                                    If IsNumeric(roles(k)) Then
                                        objTabPermission.PermissionID = intEditTabPermissionID
                                        objTabPermission.AllowAccess = True
                                        objTabPermission.RoleID = Convert.ToInt32(roles(k))
                                        objTabPermissionController.AddTabPermission(objTabPermission)
                                    End If
                                Next
                            End If
                        Next
                    Case "03.00.01"
                        Dim arrTabs As ArrayList
                        Dim objTabController As New TabController
                        arrTabs = objTabController.GetAllTabs

                        Dim TabCount As Integer
                        For TabCount = 0 To arrTabs.Count - 1
                            Dim objTab As TabInfo = CType(arrTabs(TabCount), TabInfo)
                            If Not objTab Is Nothing Then
                                objTab.TabPath = GenerateTabPath(objTab.ParentId, objTab.TabName)
                                DataProvider.Instance().UpdateTab(objTab.TabID, objTab.TabName, objTab.IsVisible, objTab.DisableLink, objTab.ParentId, objTab.IconFile, objTab.Title, objTab.Description, objTab.KeyWords, objTab.IsDeleted, objTab.Url, objTab.SkinSrc, objTab.ContainerSrc, objTab.TabPath, objTab.StartDate, objTab.EndDate)
                            End If
                        Next
                    Case "03.00.06"
                        'Need to clear the cache to pick up new HostSettings from the SQLDataProvider script
                        DataCache.RemoveCache("GetHostSettings")
                    Case "03.00.11"
                        'Need to convert any Profile Data to use XmlSerialization as Binary Formatting
                        'is not supported under Medium Trust

                        'Get all the Profiles
                        Dim objPersonalizationController As New PersonalizationController

                        Dim dr As IDataReader = DataProvider.Instance().GetAllProfiles()

                        While dr.Read
                            'Load Profile Data (using Binary Formatter method)
                            Dim objPersonalization As New PersonalizationInfo
                            Try
                                objPersonalization.UserId = Convert.ToInt32(Null.SetNull(dr("UserID"), objPersonalization.UserId))
                            Catch
                            End Try
                            Try
                                objPersonalization.PortalId = Convert.ToInt32(Null.SetNull(dr("PortalId"), objPersonalization.PortalId))
                            Catch
                            End Try
                            objPersonalization.Profile = DeserializeHashTableBase64(dr("ProfileData").ToString)
                            objPersonalization.IsModified = True

                            'Save Profile Data (using XML Serializer)
                            objPersonalizationController.SaveProfile(objPersonalization)

                        End While
                        dr.Close()
                    Case "03.00.12"
                        'If we are upgrading from a 3.0.x version then we need to upgrade the MembershipProvider
                        If upgradeMemberShipProvider Then
                            Dim strProviderPath As String = PortalSettings.GetProviderPath()
                            Dim objStreamReader As StreamReader
                            Dim strScript As String

                            'Upgrade provider
                            If writeFeedback Then
                                HtmlUtils.WriteFeedback(HttpContext.Current.Response, 0, "Executing UpgradeMembershipProvider.sql<br>")
                            End If
                            objStreamReader = File.OpenText(strProviderPath + "UpgradeMembershipProvider.sql")
                            strScript = objStreamReader.ReadToEnd
                            objStreamReader.Close()
                            strExceptions += PortalSettings.ExecuteScript(strScript)
                        End If
                    Case "03.01.00"
                        Dim objLogController As New Log.EventLog.LogController
                        Dim xmlDoc As New XmlDocument
                        Dim xmlConfigFile As String = Common.Globals.HostMapPath + "Logs\LogConfig\LogConfig.xml.resources"
                        Try
                            xmlDoc.Load(xmlConfigFile)
                        Catch exc As FileNotFoundException
                            xmlConfigFile = Common.Globals.HostMapPath + "Logs\LogConfig\LogConfigTemplate.xml.resources"
                            xmlDoc.Load(xmlConfigFile)
                        End Try
                        Dim LogType As XmlNodeList = xmlDoc.SelectNodes("/LogConfig/LogTypes/LogType")
                        Dim LogTypeInfo As XmlNode
                        For Each LogTypeInfo In LogType
                            Dim objLogTypeInfo As New Log.EventLog.LogTypeInfo
                            objLogTypeInfo.LogTypeKey = LogTypeInfo.Attributes("LogTypeKey").Value
                            objLogTypeInfo.LogTypeFriendlyName = LogTypeInfo.Attributes("LogTypeFriendlyName").Value
                            objLogTypeInfo.LogTypeDescription = LogTypeInfo.Attributes("LogTypeDescription").Value
                            objLogTypeInfo.LogTypeCSSClass = LogTypeInfo.Attributes("LogTypeCSSClass").Value
                            objLogTypeInfo.LogTypeOwner = LogTypeInfo.Attributes("LogTypeOwner").Value
                            objLogController.AddLogType(objLogTypeInfo)
                        Next

                        Dim LogTypeConfig As XmlNodeList = xmlDoc.SelectNodes("/LogConfig/LogTypeConfig")
                        Dim LogTypeConfigInfo As XmlNode
                        For Each LogTypeConfigInfo In LogTypeConfig
                            Dim objLogTypeConfig As New Log.EventLog.LogTypeConfigInfo
                            objLogTypeConfig.EmailNotificationIsActive = Convert.ToBoolean(IIf(LogTypeConfigInfo.Attributes("EmailNotificationStatus").Value = "On", True, False))
                            objLogTypeConfig.KeepMostRecent = LogTypeConfigInfo.Attributes("KeepMostRecent").Value
                            objLogTypeConfig.LoggingIsActive = Convert.ToBoolean(IIf(LogTypeConfigInfo.Attributes("LoggingStatus").Value = "On", True, False))
                            objLogTypeConfig.LogTypeKey = LogTypeConfigInfo.Attributes("LogTypeKey").Value
                            objLogTypeConfig.LogTypePortalID = LogTypeConfigInfo.Attributes("LogTypePortalID").Value
                            objLogTypeConfig.MailFromAddress = LogTypeConfigInfo.Attributes("MailFromAddress").Value
                            objLogTypeConfig.MailToAddress = LogTypeConfigInfo.Attributes("MailToAddress").Value
                            objLogTypeConfig.NotificationThreshold = Convert.ToInt32(LogTypeConfigInfo.Attributes("NotificationThreshold").Value)
                            objLogTypeConfig.NotificationThresholdTime = Convert.ToInt32(LogTypeConfigInfo.Attributes("NotificationThresholdTime").Value)
                            objLogTypeConfig.NotificationThresholdTimeType = CType(LogTypeConfigInfo.Attributes("NotificationThresholdTimeType").Value, Services.Log.EventLog.LogTypeConfigInfo.NotificationThresholdTimeTypes)
                            objLogController.AddLogTypeConfigInfo(objLogTypeConfig)
                        Next

                        Dim objScheduleItem As New Scheduling.ScheduleItem
                        objScheduleItem.TypeFullName = "DotNetNuke.Services.Cache.PurgeCache, DOTNETNUKE"
                        objScheduleItem.AttachToEvent = ""
                        objScheduleItem.CatchUpEnabled = False
                        If Common.Globals.WebFarmEnabled Then
                            objScheduleItem.Enabled = True
                        Else
                            objScheduleItem.Enabled = False
                        End If
                        objScheduleItem.ObjectDependencies = ""
                        objScheduleItem.RetainHistoryNum = 10
                        objScheduleItem.Servers = ""
                        objScheduleItem.TimeLapse = 2
                        objScheduleItem.TimeLapseMeasurement = "h"
                        objScheduleItem.RetryTimeLapse = 30
                        objScheduleItem.RetryTimeLapseMeasurement = "m"
                        Services.Scheduling.SchedulingProvider.Instance.AddSchedule(objScheduleItem)
                    Case "03.02.03"
                        'add new SecurityException
                        Dim objSecLogController As New Log.EventLog.LogController
                        Dim xmlSecDoc As New XmlDocument
                        Dim xmlSecConfigFile As String = Common.Globals.HostMapPath + "Logs\LogConfig\SecurityExceptionTemplate.xml.resources"
                        Try
                            xmlSecDoc.Load(xmlSecConfigFile)
                        Catch exc As FileNotFoundException
                            '  xmlConfigFile = Common.Globals.HostMapPath + "Logs\LogConfig\LogConfigTemplate.xml.resources"
                            ' xmlDoc.Load(xmlConfigFile)
                        End Try
                        Dim LogType As XmlNodeList = xmlSecDoc.SelectNodes("/LogConfig/LogTypes/LogType")
                        Dim LogTypeInfo As XmlNode
                        For Each LogTypeInfo In LogType
                            Dim objLogTypeInfo As New Log.EventLog.LogTypeInfo
                            objLogTypeInfo.LogTypeKey = LogTypeInfo.Attributes("LogTypeKey").Value
                            objLogTypeInfo.LogTypeFriendlyName = LogTypeInfo.Attributes("LogTypeFriendlyName").Value
                            objLogTypeInfo.LogTypeDescription = LogTypeInfo.Attributes("LogTypeDescription").Value
                            objLogTypeInfo.LogTypeCSSClass = LogTypeInfo.Attributes("LogTypeCSSClass").Value
                            objLogTypeInfo.LogTypeOwner = LogTypeInfo.Attributes("LogTypeOwner").Value
                            objSecLogController.AddLogType(objLogTypeInfo)
                        Next

                        Dim LogTypeConfig As XmlNodeList = xmlSecDoc.SelectNodes("/LogConfig/LogTypeConfig")
                        Dim LogTypeConfigInfo As XmlNode
                        For Each LogTypeConfigInfo In LogTypeConfig
                            Dim objLogTypeConfig As New Log.EventLog.LogTypeConfigInfo
                            objLogTypeConfig.EmailNotificationIsActive = Convert.ToBoolean(IIf(LogTypeConfigInfo.Attributes("EmailNotificationStatus").Value = "On", True, False))
                            objLogTypeConfig.KeepMostRecent = LogTypeConfigInfo.Attributes("KeepMostRecent").Value
                            objLogTypeConfig.LoggingIsActive = Convert.ToBoolean(IIf(LogTypeConfigInfo.Attributes("LoggingStatus").Value = "On", True, False))
                            objLogTypeConfig.LogTypeKey = LogTypeConfigInfo.Attributes("LogTypeKey").Value
                            objLogTypeConfig.LogTypePortalID = LogTypeConfigInfo.Attributes("LogTypePortalID").Value
                            objLogTypeConfig.MailFromAddress = LogTypeConfigInfo.Attributes("MailFromAddress").Value
                            objLogTypeConfig.MailToAddress = LogTypeConfigInfo.Attributes("MailToAddress").Value
                            objLogTypeConfig.NotificationThreshold = Convert.ToInt32(LogTypeConfigInfo.Attributes("NotificationThreshold").Value)
                            objLogTypeConfig.NotificationThresholdTime = Convert.ToInt32(LogTypeConfigInfo.Attributes("NotificationThresholdTime").Value)
                            objLogTypeConfig.NotificationThresholdTimeType = CType(LogTypeConfigInfo.Attributes("NotificationThresholdTimeType").Value, Services.Log.EventLog.LogTypeConfigInfo.NotificationThresholdTimeTypes)
                            objSecLogController.AddLogTypeConfigInfo(objLogTypeConfig)
                        Next

                    Case "04.04.00"
                        'Move the Export Portal Template Module Control to be a "sub-control" of portals
                        Dim objDesktopModuleController As New DesktopModuleController
                        Dim objDesktopModuleInfo As DesktopModuleInfo = objDesktopModuleController.GetDesktopModuleByModuleName("Template")

                        If Not objDesktopModuleInfo Is Nothing Then
                            Dim objModuleDefController As New ModuleDefinitionController
                            Dim objModuleDefInfo As ModuleDefinitionInfo = objModuleDefController.GetModuleDefinitionByName(objDesktopModuleInfo.DesktopModuleID, "Export Template")

                            If Not objModuleDefInfo Is Nothing Then
                                Dim objTemplateControl As ModuleControlInfo = ModuleControlController.GetModuleControlByKeyAndSrc(objModuleDefInfo.ModuleDefID, Null.NullString, "Admin/Portal/Template.ascx")

                                Dim PortalsModuleDefID As Integer = GetModuleDefinition("Portals", "Portals")

                                If PortalsModuleDefID > Null.NullInteger Then
                                    'Move Template ModuleControl to Portals Module Definition
                                    objTemplateControl.ModuleDefID = PortalsModuleDefID
                                    objTemplateControl.ControlKey = "Template"

                                    ModuleControlController.UpdateModuleControl(objTemplateControl)

                                    'Delete the Template DesktopModule
                                    objDesktopModuleController.DeleteDesktopModule(objDesktopModuleInfo.DesktopModuleID)
                                End If
                            End If
                        End If

                        ' remove module cache files with *.htm extension ( they are now securely named *.resources )
                        Dim objPortals As New PortalController
                        Dim arrPortals As ArrayList = objPortals.GetPortals
                        For Each objPortal As PortalInfo In arrPortals
                            If Directory.Exists(ApplicationMapPath & "\Portals\" & objPortal.PortalID.ToString & "\Cache\") Then
                                Dim arrFiles As String() = Directory.GetFiles(ApplicationMapPath & "\Portals\" & objPortal.PortalID.ToString & "\Cache\", "*.htm")
                                For Each strFile As String In arrFiles
                                    File.Delete(strFile)
                                Next
                            End If
                        Next

                    Case "04.07.00"
                        Dim strHostTemplateFile As String = HostMapPath & "Templates\Default.page.template"
                        If File.Exists(strHostTemplateFile) Then
                            Dim objPortals As New PortalController
                            Dim arrPortals As ArrayList = objPortals.GetPortals
                            For Each objPortal As PortalInfo In arrPortals
                                Dim strPortalTemplateFolder As String = objPortal.HomeDirectoryMapPath & "Templates\"

                                If Not Directory.Exists(strPortalTemplateFolder) Then
                                    'Create Portal Templates folder
                                    Directory.CreateDirectory(strPortalTemplateFolder)
                                End If
                                Dim strPortalTemplateFile As String = strPortalTemplateFolder + "Default.page.template"
                                If Not File.Exists(strPortalTemplateFile) Then
                                    File.Copy(strHostTemplateFile, strPortalTemplateFile)

                                    'Synchronize the Templates folder to ensure the templates are accessible
                                    FileSystemUtils.SynchronizeFolder(objPortal.PortalID, strPortalTemplateFolder, "Templates/", False, True, True)
                                End If
                            Next
                        End If
                    Case "04.08.02"
                        'checks for the very rare case where the default validationkey prior to 4.08.02
                        'is still being used and updates it
                        Config.UpdateValidationkey()
                End Select

            Catch ex As Exception

                strExceptions += "Error: " & ex.Message & vbCrLf
                Try
                    LogException(ex)
                Catch
                    ' ignore
                End Try

            End Try

            Return strExceptions

        End Function

#End Region

#Region "Public Methods"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' AddPortal manages the Installation of a new DotNetNuke Portal
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	11/06/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function AddPortal(ByVal node As XmlNode, ByVal status As Boolean, ByVal indent As Integer) As Integer
            Try
                Dim intPortalId As Integer
                Dim strHostPath As String = Common.Globals.HostMapPath
                Dim strChildPath As String = ""
                Dim strDomain As String = ""

                If Not HttpContext.Current Is Nothing Then
                    strDomain = GetDomainName(HttpContext.Current.Request, True).Replace("/Install", "")
                End If

                Dim strPortalName As String = XmlUtils.GetNodeValue(node, "portalname")
                If status Then
                    HtmlUtils.WriteFeedback(HttpContext.Current.Response, indent, "Creating Portal: " + strPortalName + "<br>")
                End If

                Dim objPortalController As New PortalController
                Dim adminNode As XmlNode = node.SelectSingleNode("administrator")
                Dim strFirstName As String = XmlUtils.GetNodeValue(adminNode, "firstname")
                Dim strLastName As String = XmlUtils.GetNodeValue(adminNode, "lastname")
                Dim strUserName As String = XmlUtils.GetNodeValue(adminNode, "username")
                Dim strPassword As String = XmlUtils.GetNodeValue(adminNode, "password")
                Dim strEmail As String = XmlUtils.GetNodeValue(adminNode, "email")
                Dim strDescription As String = XmlUtils.GetNodeValue(node, "description")
                Dim strKeyWords As String = XmlUtils.GetNodeValue(node, "keywords")
                Dim strTemplate As String = XmlUtils.GetNodeValue(node, "templatefile")
                Dim strServerPath As String = ApplicationMapPath & "\"
                Dim isChild As Boolean = Boolean.Parse(XmlUtils.GetNodeValue(node, "ischild"))
                Dim strHomeDirectory As String = XmlUtils.GetNodeValue(node, "homedirectory")

                'Get the Portal Alias
                Dim portalAliases As XmlNodeList = node.SelectNodes("portalaliases/portalalias")
                Dim strPortalAlias As String = strDomain
                If portalAliases.Count > 0 Then
                    If portalAliases(0).InnerText <> "" Then
                        strPortalAlias = portalAliases(0).InnerText
                    End If
                End If

                'Create default email
                If strEmail = "" Then
                    strEmail = "admin@" + strDomain.Replace("www.", "")
                    'Remove any domain subfolder information ( if it exists )
                    If strEmail.IndexOf("/") <> -1 Then
                        strEmail = strEmail.Substring(0, strEmail.IndexOf("/"))
                    End If
                End If

                If isChild Then
                    strChildPath = Mid(strPortalAlias, InStrRev(strPortalAlias, "/") + 1)
                End If

                'Create Portal
                intPortalId = objPortalController.CreatePortal(strPortalName, strFirstName, strLastName, strUserName, strPassword, strEmail, strDescription, strKeyWords, strHostPath, strTemplate, strHomeDirectory, strPortalAlias, strServerPath, strServerPath & strChildPath, isChild)

                If intPortalId > -1 Then
                    'Add Extra Aliases
                    For Each portalAlias As XmlNode In portalAliases
                        If portalAlias.InnerText <> "" Then
                            If status Then
                                HtmlUtils.WriteFeedback(HttpContext.Current.Response, indent, "Creating Portal Alias: " + portalAlias.InnerText + "<br>")
                            End If
                            objPortalController.AddPortalAlias(intPortalId, portalAlias.InnerText)
                        End If
                    Next

                    'Force Administrator to Update Password on first log in
                    Dim objPortal As PortalInfo = objPortalController.GetPortal(intPortalId)
                    Dim objAdminUser As UserInfo = UserController.GetUser(intPortalId, objPortal.AdministratorId, True)
                    objAdminUser.Membership.UpdatePassword = True
                    UserController.UpdateUser(intPortalId, objAdminUser)
                End If

                Return intPortalId

            Catch ex As Exception
                HtmlUtils.WriteFeedback(HttpContext.Current.Response, indent, "<font color='red'>Error: " + ex.Message + "</font><br>")
                Return -1 ' failure
            End Try
        End Function

        Public Shared Function BuildUserTable(ByVal dr As IDataReader, ByVal header As String, ByVal message As String) As String

            Dim strWarnings As String = Null.NullString
            Dim sbWarnings As New Text.StringBuilder
            Dim hasRows As Boolean = False

            sbWarnings.Append("<h3>" + header + "</h3>")
            sbWarnings.Append("<p>" + message + "</p>")
            sbWarnings.Append("<table cellspacing='4' cellpadding='4' border='0'>")
            sbWarnings.Append("<tr>")
            sbWarnings.Append("<td class='NormalBold'>ID</td>")
            sbWarnings.Append("<td class='NormalBold'>UserName</td>")
            sbWarnings.Append("<td class='NormalBold'>First Name</td>")
            sbWarnings.Append("<td class='NormalBold'>Last Name</td>")
            sbWarnings.Append("<td class='NormalBold'>Email</td>")
            sbWarnings.Append("</tr>")
            While dr.Read
                hasRows = True
                sbWarnings.Append("<tr>")
                sbWarnings.Append("<td class='Norma'>" + dr.GetInt32(0).ToString + "</td>")
                sbWarnings.Append("<td class='Norma'>" + dr.GetString(1) + "</td>")
                sbWarnings.Append("<td class='Norma'>" + dr.GetString(2) + "</td>")
                sbWarnings.Append("<td class='Norma'>" + dr.GetString(3) + "</td>")
                sbWarnings.Append("<td class='Norma'>" + dr.GetString(4) + "</td>")
                sbWarnings.Append("</tr>")
            End While

            sbWarnings.Append("</table>")

            If hasRows Then
                strWarnings = sbWarnings.ToString()
            End If

            Return strWarnings

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' CheckUpgrade checks whether there are any possible upgrade issues
        ''' </summary>
        ''' <history>
        ''' 	[cnurse]	04/11/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function CheckUpgrade() As String

            Dim dataProvider As DataProvider = Data.DataProvider.Instance()
            Dim dr As IDataReader
            Dim strWarnings As String = Null.NullString
            Dim sbWarnings As New Text.StringBuilder
            Dim hasRows As Boolean = False

            Try
                dr = dataProvider.ExecuteReader("CheckUpgrade")

                strWarnings = BuildUserTable(dr, "Duplicate SuperUsers", "We have detected that the following SuperUsers have duplicate entries as Portal Users.  Although, no longer supported, these users may have been created in early Betas of DNN v3.0.  You need to be aware that after the upgrade, these users will only be able to log in using the Super User Account's password.")

                If dr.NextResult Then
                    strWarnings += BuildUserTable(dr, "Duplicate Portal Users", "We have detected that the following Users have duplicate entries (they exist in more than one portal).  You need to be aware that after the upgrade, the password for some of these users may have been automatically changed (as the system now only uses one password per user, rather than one password per user per portal). It is important to remember that your Users can always retrieve their password using the Password Reminder feature, which will be sent to the Email addess shown in the table.")
                End If

            Catch ex As SqlException
                strWarnings += ex.Message
            Catch ex As Exception
                strWarnings += ex.Message
            End Try

            Try
                dr = dataProvider.ExecuteReader("GetUserCount")
                dr.Read()
                Dim userCount As Integer = dr.GetInt32(0)
                Dim time As Double = userCount / 10834
                If userCount > 1000 Then
                    strWarnings += "<br/><h3>More than 1000 Users</h3><p>This DotNetNuke Database has " + userCount.ToString + " users. As the users and their profiles are transferred to a new format, it is estimated that the script will take ~" + time.ToString("F2") + " minutes to execute.</p>"
                End If
            Catch ex As Exception
                strWarnings += vbCrLf + vbCrLf + ex.Message
            End Try

            Return strWarnings

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' ExecuteScripts manages the Execution of Scripts from the Install/Scripts folder.
        ''' It is also triggered by InstallDNN and UpgradeDNN
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="strProviderPath">The path to the Data Provider</param>
        ''' <history>
        ''' 	[cnurse]	05/04/2005	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub ExecuteScripts(ByVal strProviderPath As String)
            Dim arrFiles As String()
            Dim strFile As String
            Dim ScriptPath As String = ApplicationMapPath & "\Install\Scripts\"
            If Directory.Exists(ScriptPath) Then
                arrFiles = Directory.GetFiles(ScriptPath)
                For Each strFile In arrFiles
                    'Execute if script is a provider script
                    If strFile.IndexOf("." + DefaultProvider) <> -1 Then
                        ExecuteScript(strFile, "", True)
                        ' delete the file
                        Try
                            File.SetAttributes(strFile, FileAttributes.Normal)
                            File.Delete(strFile)
                        Catch
                            ' error removing the file
                        End Try
                    End If
                Next
            End If
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' ExecuteScript executes a special script
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="strFile">The script file to execute</param>
        ''' <history>
        ''' 	[cnurse]	04/11/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub ExecuteScript(ByVal strFile As String)
            'Execute if script is a provider script
            If strFile.IndexOf("." + DefaultProvider) <> -1 Then
                ExecuteScript(strFile, "", True)
            End If
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetInstallTemplate retrieves the Installation Template as specifeid in web.config
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="xmlDoc">The Xml Document to load</param>
        ''' <returns>A string which contains the error message - if appropriate</returns>
        ''' <history>
        ''' 	[cnurse]	02/13/2007	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetInstallTemplate(ByVal xmlDoc As XmlDocument) As String

            Dim strErrorMessage As String = Null.NullString
            Dim installTemplate As String = Config.GetSetting("InstallTemplate")
            Try
                xmlDoc.Load(Common.Globals.ApplicationMapPath & "\Install\" & installTemplate)
            Catch    ' error
                strErrorMessage = "Failed to load Install template.<br><br>"
            End Try

            Return strErrorMessage

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetInstallVersion retrieves the Base Instal Version as specifeid in the install
        ''' template
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="xmlDoc">The Install Template</param>
        ''' <history>
        ''' 	[cnurse]	02/13/2007	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetInstallVersion(ByVal xmlDoc As XmlDocument) As Integer()
            Dim node As XmlNode
            Dim strVersion As String = Null.NullString

            'get base version
            node = xmlDoc.SelectSingleNode("//dotnetnuke")
            If Not node Is Nothing Then
                strVersion = XmlUtils.GetNodeValue(node, "version")
            End If

            Return GetVersion(strVersion)
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetLogFile gets the filename for the version's log file
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="strProviderPath">The path to the Data Provider</param>
        '''	<param name="version">The Version</param>
        ''' <history>
        ''' 	[cnurse]	02/16/2007	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetLogFile(ByVal strProviderPath As String, ByVal version As Integer()) As String
            Dim strVersion As String = GetStringVersion(version)
            Dim logFile As String = strProviderPath + strVersion + ".log"
            Return logFile
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetScriptFile gets the filename for the version
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="strProviderPath">The path to the Data Provider</param>
        '''	<param name="version">The Version</param>
        ''' <history>
        ''' 	[cnurse]	02/16/2007	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetScriptFile(ByVal strProviderPath As String, ByVal version As Integer()) As String
            Dim strVersion As String = GetStringVersion(version)
            Dim scriptFile As String = strProviderPath + strVersion + "." + DefaultProvider
            Return scriptFile
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetStringVersion gets the Version String (xx.xx.xx) from the Version
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="version">The Version</param>
        ''' <history>
        ''' 	[cnurse]	02/15/2007	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetStringVersion(ByVal version As Integer()) As String

            Dim strVersion As String = Null.NullString
            For i As Integer = 0 To 2
                Select Case version(i)
                    Case 0
                        strVersion += "00"
                    Case 1 To 9
                        strVersion += "0" + version(i).ToString
                    Case Is >= 10
                        strVersion += version(i).ToString
                End Select
                If i < 2 Then
                    strVersion += "."
                End If

            Next

            Return strVersion
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetSuperUser gets the superuser from the Install Template
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="xmlTemplate">The install Templae</param>
        '''	<param name="writeFeedback">a flag to determine whether to output feedback</param>
        ''' <history>
        ''' 	[cnurse]	02/16/2007	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetSuperUser(ByVal xmlTemplate As XmlDocument, ByVal writeFeedback As Boolean) As UserInfo
            Dim node As XmlNode = xmlTemplate.SelectSingleNode("//dotnetnuke/superuser")
            Dim objSuperUserInfo As UserInfo = Nothing
            If Not node Is Nothing Then
                If writeFeedback Then
                    HtmlUtils.WriteFeedback(HttpContext.Current.Response, 0, "Configuring SuperUser:<br>")
                End If

                'Parse the SuperUsers nodes
                Dim strFirstName As String = XmlUtils.GetNodeValue(node, "firstname")
                Dim strLastName As String = XmlUtils.GetNodeValue(node, "lastname")
                Dim strUserName As String = XmlUtils.GetNodeValue(node, "username")
                Dim strPassword As String = XmlUtils.GetNodeValue(node, "password")
                Dim strEmail As String = XmlUtils.GetNodeValue(node, "email")
                Dim strLocale As String = XmlUtils.GetNodeValue(node, "locale")
                Dim timeZone As Integer = XmlUtils.GetNodeValueInt(node, "timezone")

                objSuperUserInfo = New UserInfo
                objSuperUserInfo.PortalID = -1
                objSuperUserInfo.FirstName = strFirstName
                objSuperUserInfo.LastName = strLastName
                objSuperUserInfo.Username = strUserName
                objSuperUserInfo.DisplayName = strFirstName + " " + strLastName
                objSuperUserInfo.Membership.Password = strPassword
                objSuperUserInfo.Email = strEmail
                objSuperUserInfo.IsSuperUser = True
                objSuperUserInfo.Membership.Approved = True

                objSuperUserInfo.Profile.FirstName = strFirstName
                objSuperUserInfo.Profile.LastName = strLastName
                objSuperUserInfo.Profile.PreferredLocale = strLocale
                objSuperUserInfo.Profile.TimeZone = timeZone
            End If
            Return objSuperUserInfo
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetUpgradeScripts gets an ArrayList of the Scripts required to Upgrade to the
        ''' current Assembly Version
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="strProviderPath">The path to the Data Provider</param>
        '''	<param name="dbVersion">The current Database Version</param>
        ''' <history>
        ''' 	[cnurse]	02/15/2007	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetUpgradeScripts(ByVal strProviderPath As String, ByVal dbVersion As Integer()) As ArrayList
            Return GetUpgradeScripts(strProviderPath, GetStringVersion(dbVersion).Replace(".", ""))
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetUpgradeScripts gets an ArrayList of the Scripts required to Upgrade to the
        ''' current Assembly Version
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="strProviderPath">The path to the Data Provider</param>
        '''	<param name="strDatabaseVersion">The current Database Version</param>
        ''' <history>
        ''' 	[cnurse]	02/14/2007	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetUpgradeScripts(ByVal strProviderPath As String, ByVal strDatabaseVersion As String) As ArrayList
            Dim strScriptVersion As String
            Dim arrScriptFiles As New ArrayList
            Dim strFile As String
            Dim arrFiles As String() = Directory.GetFiles(strProviderPath, "*." & DefaultProvider)
            For Each strFile In arrFiles
                ' script file name must conform to ##.##.##.DefaultProviderName
                If Len(Path.GetFileName(strFile)) = 9 + Len(DefaultProvider) Then
                    strScriptVersion = Path.GetFileNameWithoutExtension(strFile)
                    ' check if script file is relevant for upgrade
                    If strScriptVersion.Replace(".", "") > strDatabaseVersion And strScriptVersion.Replace(".", "") <= glbAppVersion.Replace(".", "") Then
                        arrScriptFiles.Add(strFile)
                    End If
                End If
            Next
            arrScriptFiles.Sort()

            Return arrScriptFiles

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetVersion gets the Version from the Version String
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="strVersion">The Version String</param>
        ''' <history>
        ''' 	[cnurse]	02/13/2007	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetVersion(ByVal strVersion As String) As Integer()
            Dim intVersion(2) As Integer
            If Not String.IsNullOrEmpty(strVersion) Then
                Dim arrVersion As Array = strVersion.Split(CType(".", Char))
                intVersion(0) = CType(arrVersion.GetValue((0)), Integer)
                intVersion(1) = CType(arrVersion.GetValue((1)), Integer)
                intVersion(2) = CType(arrVersion.GetValue((2)), Integer)
            End If
            Return intVersion
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' InitialiseHostSettings gets the Host Settings from the Install Template
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="xmlTemplate">The install Templae</param>
        '''	<param name="writeFeedback">a flag to determine whether to output feedback</param>
        ''' <history>
        ''' 	[cnurse]	02/16/2007	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub InitialiseHostSettings(ByVal xmlTemplate As XmlDocument, ByVal writeFeedback As Boolean)
            Dim node As XmlNode = xmlTemplate.SelectSingleNode("//dotnetnuke/settings")
            If Not node Is Nothing Then
                If writeFeedback Then
                    HtmlUtils.WriteFeedback(HttpContext.Current.Response, 0, "Loading Host Settings:<br>")
                End If

                Dim settingNode As XmlNode
                Dim objController As New HostSettingsController

                'Parse the Settings nodes
                For Each settingNode In node.ChildNodes
                    Dim strSettingName As String = settingNode.Name
                    Dim strSettingValue As String = settingNode.InnerText
                    Dim SecureAttrib As XmlAttribute = settingNode.Attributes("Secure")
                    Dim SettingIsSecure As Boolean = False
                    If Not SecureAttrib Is Nothing Then
                        If SecureAttrib.Value.ToLower = "true" Then
                            SettingIsSecure = True
                        End If
                    End If

                    Dim strDomainName As String = GetDomainName(HttpContext.Current.Request)

                    Select Case strSettingName
                        Case "HostURL"
                            If strSettingValue = "" Then
                                strSettingValue = strDomainName
                            End If
                        Case "HostEmail"
                            If strSettingValue = "" Then
                                strSettingValue = "support@" + strDomainName

                                'Remove any folders
                                strSettingValue = strSettingValue.Substring(0, strSettingValue.IndexOf("/"))
                            End If

                    End Select

                    objController.UpdateHostSetting(strSettingName, strSettingValue, SettingIsSecure)

                Next
                'Need to clear the cache to pick up new HostSettings from the SQLDataProvider script
                DataCache.RemoveCache("GetHostSettings")
            End If
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' InstallDatabase runs all the "scripts" identifed in the Install Template to 
        ''' install the base version
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="xmlDoc">The Xml Document to load</param>
        ''' <param name="writeFeedback">A flag that determines whether to output feedback to the Response Stream</param>
        ''' <returns>A string which contains the error message - if appropriate</returns>
        ''' <history>
        ''' 	[cnurse]	02/13/2007	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function InstallDatabase(ByVal intVersion As Integer(), ByVal strProviderPath As String, ByVal xmlDoc As XmlDocument, ByVal writeFeedback As Boolean) As String
            Dim node As XmlNode
            Dim strScript As String = Null.NullString
            Dim strDefaultProvider As String = Config.GetDefaultProvider("data").Name
            Dim strMessage As String = Null.NullString

            'Output feedback line
            If writeFeedback Then
                HtmlUtils.WriteFeedback(HttpContext.Current.Response, 0, "Installing Version: " + intVersion(0).ToString + "." + intVersion(1).ToString + "." + intVersion(2).ToString + "<br>")
            End If

            'Parse the script nodes
            node = xmlDoc.SelectSingleNode("//dotnetnuke/scripts")
            If Not node Is Nothing Then
                ' Loop through the available scripts
                For Each scriptNode As XmlNode In node.SelectNodes("script")
                    strScript = scriptNode.InnerText + "." + strDefaultProvider
                    strMessage += ExecuteScript(strProviderPath & strScript, "", writeFeedback)
                Next
            End If

            ' update the version
            PortalSettings.UpdateDatabaseVersion(intVersion(0), intVersion(1), intVersion(2))

            'Optionally Install the memberRoleProvider
            strMessage += InstallMemberRoleProvider(strProviderPath, writeFeedback)

            Return strMessage
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' InstallDNN manages the Installation of a new DotNetNuke Application
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="strProviderPath">The path to the Data Provider</param>
        ''' <history>
        ''' 	[cnurse]	11/06/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub InstallDNN(ByVal strProviderPath As String)

            Dim strExceptions As String = ""
            Dim intPortalId As Integer
            Dim strHostPath As String = Common.Globals.HostMapPath
            Dim xmlDoc As New XmlDocument
            Dim node As XmlNode
            Dim nodes As XmlNodeList
            Dim strVersion As String = ""
            Dim strScript As String = ""
            Dim strLogFile As String = ""
            Dim strErrorMessage As String = ""

            ' get current App version from constant (Medium Trust)
            Dim strAssemblyVersion As String = glbAppVersion.Replace(".", "")

            ' open the Install Template XML file
            strErrorMessage = GetInstallTemplate(xmlDoc)

            If strErrorMessage = "" Then
                'get base version
                node = xmlDoc.SelectSingleNode("//dotnetnuke")
                If Not node Is Nothing Then
                    strVersion = XmlUtils.GetNodeValue(node, "version")
                End If

                'get base version
                Dim intVersion() As Integer = GetVersion(strVersion)

                'Install Base Version
                strErrorMessage = InstallDatabase(intVersion, strProviderPath, xmlDoc, True)

                'Call Upgrade with the current DB Version to carry out any incremental upgrades
                UpgradeDNN(strProviderPath, strVersion.Replace(".", ""))

                ' parse Host Settings if available
                InitialiseHostSettings(xmlDoc, True)

                ' parse SuperUser if Available
                Dim superUser As UserInfo = GetSuperUser(xmlDoc, True)
                If superUser.Membership.Password.Contains("host") Then
                    superUser.Membership.UpdatePassword = True
                End If
                UserController.CreateUser(superUser)

                ' parse File List if available
                InstallFiles(xmlDoc, True)

                'Install modules if present
                HtmlUtils.WriteFeedback(HttpContext.Current.Response, 0, "Installing Modules:<br>")
                ResourceInstaller.Install(True, 2, "modules")

                'Run any addition scripts in the Scripts folder
                HtmlUtils.WriteFeedback(HttpContext.Current.Response, 0, "Executing Additional Scripts:<br>")
                ExecuteScripts(strProviderPath)

                ' parse portal(s) if available
                nodes = xmlDoc.SelectNodes("//dotnetnuke/portals/portal")
                For Each node In nodes
                    If Not node Is Nothing Then
                        intPortalId = AddPortal(node, True, 2)
                        If intPortalId > -1 Then
                            HtmlUtils.WriteFeedback(HttpContext.Current.Response, 2, "<font color='green'>Successfully Installed Portal " & intPortalId & ":</font><br>")
                        Else
                            HtmlUtils.WriteFeedback(HttpContext.Current.Response, 2, "<font color='red'>Portal failed to install:Error!</font><br>")
                        End If
                    End If
                Next

                'Install optional resources if present
                HtmlUtils.WriteFeedback(HttpContext.Current.Response, 0, "Installing Optional Resources:<br>")
                ResourceInstaller.Install(True, 2)

            Else
                '500 Error - Redirect to ErrorPage
                If Not HttpContext.Current Is Nothing Then
                    Dim strURL As String = "~/ErrorPage.aspx?status=500&error=" & strErrorMessage
                    HttpContext.Current.Response.Clear()
                    HttpContext.Current.Server.Transfer(strURL)
                End If
            End If
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' InstallFiles intsalls any files listed in the Host Install Configuration file
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="xmlDoc">The Xml Document to load</param>
        ''' <param name="writeFeedback">A flag that determines whether to output feedback to the Response Stream</param>
        ''' <history>
        ''' 	[cnurse]	02/19/2007	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub InstallFiles(ByVal xmlDoc As XmlDocument, ByVal writeFeedback As Boolean)
            Dim node As XmlNode

            'Parse the file nodes
            node = xmlDoc.SelectSingleNode("//dotnetnuke/files")
            If Not node Is Nothing Then
                If writeFeedback Then
                    HtmlUtils.WriteFeedback(HttpContext.Current.Response, 0, "Loading Host Files:<br>")
                End If
                ParseFiles(node, Null.NullInteger)
            End If

            'Synchronise Host Folder
            If writeFeedback Then
                HtmlUtils.WriteFeedback(HttpContext.Current.Response, 0, "Synchronizing Host Files:<br>")
            End If
            FileSystemUtils.SynchronizeFolder(Null.NullInteger, Common.Globals.HostMapPath, "", True, False, True)

        End Sub

        Public Shared Sub StartTimer()

            'Start Upgrade Timer
            startTime = Now()

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' UpgradeDNN manages the Upgrade of an exisiting DotNetNuke Application
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="strProviderPath">The path to the Data Provider</param>
        '''	<param name="strDatabaseVersion">The current Database Version</param>
        ''' <history>
        ''' 	[cnurse]	11/06/2004	created (Upgrade code extracted from AutoUpgrade)
        '''     [cnurse]    11/10/2004  version specific upgrades extracted to ExecuteScript
        '''     [cnurse]    01/20/2005  changed to Public so Upgrade can be manually controlled
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub UpgradeDNN(ByVal strProviderPath As String, ByVal strDatabaseVersion As String)
            'get assembly version
            Dim assemblyVersion() As Integer = GetVersion(glbAppVersion)
            HtmlUtils.WriteFeedback(HttpContext.Current.Response, 0, "Upgrading to Version: " + assemblyVersion(0).ToString + "." + assemblyVersion(1).ToString + "." + assemblyVersion(2).ToString + "<br/>")

            'Process the Upgrade Script files
            For Each strScriptFile As String In GetUpgradeScripts(strProviderPath, strDatabaseVersion)
                UpgradeVersion(strScriptFile, True)
            Next

            ' perform general application upgrades
            HtmlUtils.WriteFeedback(HttpContext.Current.Response, 0, "Performing General Upgrades<br>")
            UpgradeApplication()

            DataCache.ClearHostCache(True)
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' UpgradeVersion upgrades a single version
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="strScriptFile">The upgrade script file</param>
        ''' <history>
        ''' 	[cnurse]	02/14/2007	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub UpgradeVersion(ByVal strScriptFile As String, ByVal writeFeedback As Boolean)
            Dim strScriptVersion As String = Path.GetFileNameWithoutExtension(strScriptFile)
            Dim version() As Integer = GetVersion(strScriptVersion)
            Dim strExceptions As String

            ' verify script has not already been run
            If Not PortalSettings.FindDatabaseVersion(version(0), version(1), version(2)) Then
                ' upgrade database schema
                PortalSettings.UpgradeDatabaseSchema(version(0), version(1), version(2))

                ' execute script file (and version upgrades) for version
                strExceptions = ExecuteScript(strScriptFile, strScriptVersion, writeFeedback)

                ' update the version
                PortalSettings.UpdateDatabaseVersion(version(0), version(1), version(2))

                Dim objEventLog As New Services.Log.EventLog.EventLogController
                Dim objEventLogInfo As New Services.Log.EventLog.LogInfo
                objEventLogInfo.AddProperty("Upgraded DotNetNuke", "Version: " + version(0).ToString + "." + version(1).ToString + "." + version(2).ToString)
                If strExceptions.Length > 0 Then
                    objEventLogInfo.AddProperty("Warnings", strExceptions)
                Else
                    objEventLogInfo.AddProperty("No Warnings", "")
                End If
                objEventLogInfo.LogTypeKey = Services.Log.EventLog.EventLogController.EventLogType.HOST_ALERT.ToString
                objEventLogInfo.BypassBuffering = True
                'objEventLog.AddLog(objEventLogInfo)
            End If
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' UpgradeApplication - This overload is used for general application upgrade operations. 
        ''' </summary>
        ''' <remarks>
        '''	Since it is not version specific and is invoked whenever the application is 
        '''	restarted, the operations must be re-executable.
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	11/6/2004	documented
        '''     [cnurse]    02/27/2007  made public so it can be called from Wizard
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub UpgradeApplication()

            Dim objTabController As New TabController
            Dim HostPage As TabInfo = objTabController.GetTabByName("Host", Null.NullInteger)
            Dim newPage As TabInfo

            Dim ModuleDefID As Integer

            Try
                ' remove the system message module from the admin tab
                ' System Messages are now managed through Localization
                If CoreModuleExists("System Messages") Then
                    RemoveCoreModule("System Messages", "Admin", "Site Settings", False)
                End If

                ' add the log viewer module to the admin tab
                If CoreModuleExists("Log Viewer") = False Then
                    ModuleDefID = AddModuleDefinition("Log Viewer", "Allows you to view log entries for portal events.", "Log Viewer")
                    AddModuleControl(ModuleDefID, "", "", "Admin/Logging/LogViewer.ascx", "", SecurityAccessLevel.Admin, 0)
                    AddModuleControl(ModuleDefID, "Edit", "Edit Log Settings", "Admin/Logging/EditLogTypes.ascx", "", SecurityAccessLevel.Host, 0)

                    'Add the Module/Page to all configured portals
                    AddAdminPages("Log Viewer", "icon_viewstats_16px.gif", True, ModuleDefID, "Log Viewer", "icon_viewstats_16px.gif")
                End If

                If CoreModuleExists("Windows Authentication") = False Then
                    ModuleDefID = AddModuleDefinition("Windows Authentication", "Allows you to manage authentication settings for sites using Windows Authentication.", "Windows Authentication")
                    AddModuleControl(ModuleDefID, "", "", "Admin/Security/AuthenticationSettings.ascx", "", SecurityAccessLevel.Admin, 0)

                    'Add the Module/Page to all configured portals
                    AddAdminPages("Authentication", "icon_authentication_16px.gif", True, ModuleDefID, "Authentication", "icon_authentication_16px.gif")
                End If

                ' add the schedule module to the host tab
                If CoreModuleExists("Schedule") = False Then
                    ModuleDefID = AddModuleDefinition("Schedule", "Allows you to schedule tasks to be run at specified intervals.", "Schedule")
                    AddModuleControl(ModuleDefID, "", "", "Admin/Scheduling/ViewSchedule.ascx", "", SecurityAccessLevel.Admin, 0)
                    AddModuleControl(ModuleDefID, "Edit", "Edit Schedule", "Admin/Scheduling/EditSchedule.ascx", "", SecurityAccessLevel.Host, 0)
                    AddModuleControl(ModuleDefID, "History", "Schedule History", "Admin/Scheduling/ViewScheduleHistory.ascx", "", SecurityAccessLevel.Host, 0)
                    AddModuleControl(ModuleDefID, "Status", "Schedule Status", "Admin/Scheduling/ViewScheduleStatus.ascx", "", SecurityAccessLevel.Host, 0)

                    'Create New Host Page (or get existing one)
                    newPage = AddHostPage("Schedule", "icon_scheduler_16px.gif", True)

                    'Add Module To Page
                    AddModuleToPage(newPage, ModuleDefID, "Schedule", "icon_scheduler_16px.gif")
                End If

                ' add the skins module to the admin tab
                If CoreModuleExists("Skins") = False Then
                    ModuleDefID = AddModuleDefinition("Skins", "Allows you to manage your skins and containers.", "Skins")
                    AddModuleControl(ModuleDefID, "", "", "Admin/Skins/EditSkins.ascx", "", SecurityAccessLevel.Admin, 0)

                    'Add the Module/Page to all configured portals
                    AddAdminPages("Skins", "icon_skins_16px.gif", True, ModuleDefID, "Skins", "icon_skins_16px.gif")

                End If

                ' add the language editor module to the host tab
                If Not CoreModuleExists("Languages") Then
                    ModuleDefID = AddModuleDefinition("Languages", "The Super User can manage the suported languages installed on the system.", "Languages")
                    AddModuleControl(ModuleDefID, "", "", "Admin/Localization/Languages.ascx", "", SecurityAccessLevel.Host, 0)
                    AddModuleControl(ModuleDefID, "TimeZone", "TimeZone Editor", "Admin/Localization/TimeZoneEditor.ascx", "", SecurityAccessLevel.Host, 0)
                    AddModuleControl(ModuleDefID, "Language", "Language Editor", "Admin/Localization/LanguageEditor.ascx", "", SecurityAccessLevel.Host, 0)
                    AddModuleControl(ModuleDefID, "FullEditor", "Language Editor", "Admin/Localization/LanguageEditorExt.ascx", "", SecurityAccessLevel.Host, 0)
                    AddModuleControl(ModuleDefID, "Verify", "Resource File Verifier", "Admin/Localization/ResourceVerifier.ascx", "", SecurityAccessLevel.Host, 0)
                    AddModuleControl(ModuleDefID, "Package", "Create Language Pack", "Admin/Localization/LanguagePack.ascx", "", SecurityAccessLevel.Host, 0)

                    'Create New Host Page (or get existing one)
                    newPage = AddHostPage("Languages", "icon_language_16px.gif", True)

                    'Add Module To Page
                    AddModuleToPage(newPage, ModuleDefID, "Languages", "icon_language_16px.gif")

                    ModuleDefID = AddModuleDefinition("Custom Locales", "Administrator can manage custom translations for portal.", "Custom Portal Locale")
                    AddModuleControl(ModuleDefID, "", "", "Admin/Localization/LanguageEditor.ascx", "", SecurityAccessLevel.Admin, 0)
                    AddModuleControl(ModuleDefID, "FullEditor", "Language Editor", "Admin/Localization/LanguageEditorExt.ascx", "", SecurityAccessLevel.Admin, 0)

                    'Add the Module/Page to all configured portals
                    AddAdminPages("Languages", "icon_language_16px.gif", True, ModuleDefID, "Languages", "icon_language_16px.gif")
                End If

                ' add the Search Admin module to the host tab
                If CoreModuleExists("Search Admin") = False Then
                    ModuleDefID = AddModuleDefinition("Search Admin", "The Search Admininstrator provides the ability to manage search settings.", "Search Admin")
                    AddModuleControl(ModuleDefID, "", "", "Admin/Search/SearchAdmin.ascx", "", SecurityAccessLevel.Host, 0)

                    'Create New Host Page (or get existing one)
                    newPage = AddHostPage("Search Admin", "icon_search_16px.gif", True)

                    'Add Module To Page
                    AddModuleToPage(newPage, ModuleDefID, "Search Admin", "icon_search_16px.gif")

                    'Add the Module/Page to all configured portals
                    'AddAdminPages("Search Admin", "icon_search_16px.gif", True, ModuleDefID, "Search Admin", "icon_search_16px.gif")
                End If

                ' add the Search Input module
                If CoreModuleExists("Search Input") = False Then
                    ModuleDefID = AddModuleDefinition("Search Input", "The Search Input module provides the ability to submit a search to a given search results module.", "Search Input", False, False)
                    AddModuleControl(ModuleDefID, "", "", "DesktopModules/SearchInput/SearchInput.ascx", "", SecurityAccessLevel.Anonymous, 0)
                    AddModuleControl(ModuleDefID, "Settings", "Search Input Settings", "DesktopModules/SearchInput/Settings.ascx", "", SecurityAccessLevel.Edit, 0)
                End If

                ' add the Search Results module
                If CoreModuleExists("Search Results") = False Then
                    ModuleDefID = AddModuleDefinition("Search Results", "The Search Reasults module provides the ability to display search results.", "Search Results", False, False)
                    AddModuleControl(ModuleDefID, "", "", "DesktopModules/SearchResults/SearchResults.ascx", "", SecurityAccessLevel.Anonymous, 0)
                    AddModuleControl(ModuleDefID, "Settings", "Search Results Settings", "DesktopModules/SearchResults/Settings.ascx", "", SecurityAccessLevel.Edit, 0)

                    'Add the Search Module/Page to all configured portals
                    AddSearchResults(ModuleDefID)
                End If

                ' add the site wizard module to the admin tab 
                If CoreModuleExists("Site Wizard") = False Then
                    ModuleDefID = AddModuleDefinition("Site Wizard", "The Administrator can use this user-friendly wizard to set up the common features of the Portal/Site.", "Site Wizard")
                    AddModuleControl(ModuleDefID, "", "", "Admin/Portal/Sitewizard.ascx", "", SecurityAccessLevel.Admin, 0)
                    AddAdminPages("Site Wizard", "icon_wizard_16px.gif", True, ModuleDefID, "Site Wizard", "icon_wizard_16px.gif")
                End If

                ' add portal alias module
                If CoreModuleExists("Portal Aliases") = False Then
                    ModuleDefID = AddModuleDefinition("Portal Aliases", "Allows you to view portal aliases.", "Portal Aliases")
                    AddModuleControl(ModuleDefID, "", "", "Admin/Portal/PortalAlias.ascx", "", SecurityAccessLevel.Host, 0)
                    AddModuleControl(ModuleDefID, "Edit", "Portal Aliases", "Admin/Portal/EditPortalAlias.ascx", "", SecurityAccessLevel.Host, 0)

                    'Add the Module/Page to all configured portals (with InheritViewPermissions = False)
                    AddAdminPages("Site Settings", "icon_sitesettings_16px.gif", False, ModuleDefID, "Portal Aliases", "icon_sitesettings_16px.gif", False)
                End If

                'add Lists module and tab
                If HostTabExists("Lists") = False Then
                    ModuleDefID = AddModuleDefinition("Lists", "Allows you to edit common lists.", "Lists")
                    AddModuleControl(ModuleDefID, "", "", "Admin/Lists/ListEditor.ascx", "", SecurityAccessLevel.Host, 0)

                    'Create New Host Page (or get existing one)
                    newPage = AddHostPage("Lists", "icon_lists_16px.gif", True)

                    'Add Module To Page
                    AddModuleToPage(newPage, ModuleDefID, "Lists", "icon_lists_16px.gif")
                End If

                If HostTabExists("Superuser Accounts") = False Then
                    'add SuperUser Accounts module and tab
                    Dim objDesktopModuleController As New DesktopModuleController
                    Dim objDesktopModuleInfo As DesktopModuleInfo
                    objDesktopModuleInfo = objDesktopModuleController.GetDesktopModuleByModuleName("User Accounts")
                    Dim objModuleDefController As New ModuleDefinitionController
                    ModuleDefID = objModuleDefController.GetModuleDefinitionByName(objDesktopModuleInfo.DesktopModuleID, "User Accounts").ModuleDefID

                    'Create New Host Page (or get existing one)
                    newPage = AddHostPage("Superuser Accounts", "icon_users_16px.gif", True)

                    'Add Module To Page
                    AddModuleToPage(newPage, ModuleDefID, "Superuser Accounts", "icon_users_32px.gif")
                End If

                'add Skins module and tab to Host menu
                If HostTabExists("Skins") = False Then
                    Dim objDesktopModuleController As New DesktopModuleController
                    Dim objDesktopModuleInfo As DesktopModuleInfo
                    objDesktopModuleInfo = objDesktopModuleController.GetDesktopModuleByModuleName("Skins")
                    Dim objModuleDefController As New ModuleDefinitionController
                    ModuleDefID = objModuleDefController.GetModuleDefinitionByName(objDesktopModuleInfo.DesktopModuleID, "Skins").ModuleDefID

                    'Create New Host Page (or get existing one)
                    newPage = AddHostPage("Skins", "icon_skins_16px.gif", True)

                    'Add Module To Page
                    AddModuleToPage(newPage, ModuleDefID, "Skins", "")
                End If


                'Add Search Skin Object
                AddModuleControl(Null.NullInteger, "SEARCH", Null.NullString, "Admin/Skins/Search.ascx", "", SecurityAccessLevel.SkinObject, Null.NullInteger)

                'Add TreeView Skin Object
                AddModuleControl(Null.NullInteger, "TREEVIEW", Null.NullString, "Admin/Skins/TreeViewMenu.ascx", "", SecurityAccessLevel.SkinObject, Null.NullInteger)

                'Add Text Skin Object
                AddModuleControl(Null.NullInteger, "TEXT", Null.NullString, "Admin/Skins/Text.ascx", "", SecurityAccessLevel.SkinObject, Null.NullInteger)

                'Add Styles Skin Object
                AddModuleControl(Null.NullInteger, "STYLES", Null.NullString, "Admin/Skins/Styles.ascx", "", SecurityAccessLevel.SkinObject, Null.NullInteger)

                'Add Private Assembly Packager
                ModuleDefID = GetModuleDefinition("Module Definitions", "Module Definitions")
                AddModuleControl(ModuleDefID, "Package", "Create Private Assembly", "Admin/ModuleDefinitions/PrivateAssembly.ascx", "icon_moduledefinitions_32px.gif", SecurityAccessLevel.Edit, Null.NullInteger)

                'Add Edit Role Groups
                ModuleDefID = GetModuleDefinition("Security Roles", "Security Roles")
                AddModuleControl(ModuleDefID, "EditGroup", "Edit Role Groups", "Admin/Security/EditGroups.ascx", "icon_securityroles_32px.gif", SecurityAccessLevel.Edit, Null.NullInteger)
                AddModuleControl(ModuleDefID, "UserSettings", "Manage User Settings", "Admin/Users/UserSettings.ascx", "~/images/settings.gif", SecurityAccessLevel.Edit, Null.NullInteger)

                'Add User Accounts Controls
                ModuleDefID = GetModuleDefinition("User Accounts", "User Accounts")
                AddModuleControl(ModuleDefID, "ManageProfile", "Manage Profile Definition", "Admin/Users/ProfileDefinitions.ascx", "icon_users_32px.gif", SecurityAccessLevel.Edit, Null.NullInteger)
                AddModuleControl(ModuleDefID, "EditProfileProperty", "Edit Profile Property Definition", "Admin/Users/EditProfileDefinition.ascx", "icon_users_32px.gif", SecurityAccessLevel.Edit, Null.NullInteger)
                AddModuleControl(ModuleDefID, "UserSettings", "Manage User Settings", "Admin/Users/UserSettings.ascx", "~/images/settings.gif", SecurityAccessLevel.Edit, Null.NullInteger)
                AddModuleControl(Null.NullInteger, "Profile", "Profile", "Admin/Users/ManageUsers.ascx", "icon_users_32px.gif", SecurityAccessLevel.Anonymous, Null.NullInteger)
                AddModuleControl(Null.NullInteger, "SendPassword", "Send Password", "Admin/Security/SendPassword.ascx", "", SecurityAccessLevel.Anonymous, Null.NullInteger)
                AddModuleControl(Null.NullInteger, "ViewProfile", "View Profile", "Admin/Users/ViewProfile.ascx", "icon_users_32px.gif", SecurityAccessLevel.Anonymous, Null.NullInteger)

                'Update Child Portal subHost.aspx
                Dim objAliasController As New PortalAliasController
                Dim arrAliases As ArrayList = objAliasController.GetPortalAliasArrayByPortalID(Null.NullInteger)
                Dim objAlias As PortalAliasInfo
                Dim childPath As String

                For Each objAlias In arrAliases
                    'For the alias to be for a child it must be of the form ...../child
                    If objAlias.HTTPAlias.LastIndexOf("/") <> -1 Then
                        childPath = ApplicationMapPath & "\" & objAlias.HTTPAlias.Substring(objAlias.HTTPAlias.LastIndexOf("/") + 1)
                        If Directory.Exists(childPath) Then
                            'Folder exists App/child so upgrade

                            'Rename existing file 
                            System.IO.File.Copy(childPath & "\" & glbDefaultPage, childPath & "\old_" & glbDefaultPage, True)

                            ' create the subhost default.aspx file
                            System.IO.File.Copy(Common.Globals.HostMapPath & "subhost.aspx", childPath & "\" & glbDefaultPage, True)
                        End If
                    End If
                Next

                ' add the solutions explorer module to the admin tab 
                If CoreModuleExists("Solutions") = False Then
                    ModuleDefID = AddModuleDefinition("Solutions", "Browse additional solutions for your application.", "Solutions")
                    AddModuleControl(ModuleDefID, "", "", "Admin/Host/Solutions.ascx", "", SecurityAccessLevel.Admin, 0)
                    AddAdminPages("Solutions", "icon_solutions_16px.gif", True, ModuleDefID, "Solutions Explorer", "icon_solutions_32px.gif")
                End If

                ' add skin designer module to host / skins page 
                If CoreModuleExists("Skin Designer") = False Then
                    ModuleDefID = AddModuleDefinition("Skin Designer", "Allows you to modify skin attributes.", "Skin Designer")
                    AddModuleControl(ModuleDefID, "", "", "Admin/Skins/Attributes.ascx", "", SecurityAccessLevel.Host, 0)
                    newPage = AddHostPage("Skins", "icon_skins_16px.gif", True)
                    AddModuleToPage(newPage, ModuleDefID, "Skin Designer", "icon_skins_32px.gif")
                End If

                ' add the what's wew module to the admin tab
                If CoreModuleExists("WhatsNew") = False Then
                    ModuleDefID = AddModuleDefinition("WhatsNew", "Displays What's New information for each release.", "WhatsNew")
                    AddModuleControl(ModuleDefID, "", "", "Admin/Portal/WhatsNew.ascx", "", SecurityAccessLevel.Admin, 0)

                    'Add the Module/Page to all configured portals
                    AddAdminPages("What's New", "icon_whatsnew_16px.gif", True, ModuleDefID, "What's New", "icon_whatsnew_32px.gif")

                End If

            Catch ex As Exception
                Dim objEventLog As New Services.Log.EventLog.EventLogController
                Dim objEventLogInfo As New Services.Log.EventLog.LogInfo
                objEventLogInfo.AddProperty("Upgraded DotNetNuke", "General")
                objEventLogInfo.AddProperty("Warnings", "Error: " & ex.Message & vbCrLf)
                objEventLogInfo.LogTypeKey = Services.Log.EventLog.EventLogController.EventLogType.HOST_ALERT.ToString
                objEventLogInfo.BypassBuffering = True
                objEventLog.AddLog(objEventLogInfo)
                Try
                    LogException(ex)
                Catch
                    ' ignore
                End Try

            End Try

            'Remove any .txt and .config files that may exist in the Host folder
            For Each strFile As String In Directory.GetFiles(HostMapPath, "??.??.??.txt")
                FileSystemUtils.DeleteFile(strFile)
            Next
            For Each strFile As String In Directory.GetFiles(HostMapPath, "??.??.??.config")
                FileSystemUtils.DeleteFile(strFile)
            Next

        End Sub

        Public Shared Function UpgradeIndicator(ByVal Version As String, ByVal IsLocal As Boolean, ByVal IsSecureConnection As Boolean) As String
            Return UpgradeIndicator(Version, "~", IsLocal, IsSecureConnection)
        End Function

        Public Shared Function UpgradeIndicator(ByVal Version As String, ByVal ModuleName As String, ByVal IsLocal As Boolean, ByVal IsSecureConnection As Boolean) As String
            Return UpgradeIndicator(Version, "~", "", IsLocal, IsSecureConnection)
        End Function

        Public Shared Function UpgradeIndicator(ByVal Version As String, ByVal ModuleName As String, ByVal Culture As String, ByVal IsLocal As Boolean, ByVal IsSecureConnection As Boolean) As String
            Dim strURL As String = ""
            Dim strCheckUpgrade As String = Convert.ToString(Common.Globals.HostSettings("CheckUpgrade"))
            If strCheckUpgrade <> "N" And Version <> "" And IsLocal = False And IsSecureConnection = False Then
                strURL = glbUpgradeUrl & "/update.aspx"
                strURL += "?core=" & glbAppVersion.Replace(".", "")
                strURL += "&version=" & Version.Replace(".", "")
                If ModuleName <> "~" Then
                    strURL += "&modulename=" & ModuleName
                Else
                    strURL += "&modulename=" & glbAppName
                End If
                If Culture <> "" Then
                    strURL += "&culture=" & Culture
                End If
            End If
            Return strURL
        End Function

        Public Shared Function UpgradeRedirect() As String
            Return UpgradeRedirect("~")
        End Function

        Public Shared Function UpgradeRedirect(ByVal ModuleName As String) As String
            Return UpgradeRedirect(glbAppVersion, ModuleName, "")
        End Function

        Public Shared Function UpgradeRedirect(ByVal Version As String, ByVal ModuleName As String, ByVal Culture As String) As String
            Dim strURL As String = ""
            strURL = glbUpgradeUrl & "/redirect.aspx"
            strURL += "?core=" & glbAppVersion.Replace(".", "")
            strURL += "&version=" & Version.Replace(".", "")
            If ModuleName <> "~" Then
                strURL += "&modulename=" & ModuleName
            Else
                strURL += "&modulename=" & glbAppName
            End If
            If Culture <> "" Then
                strURL += "&culture=" & Culture
            End If
            Return strURL
        End Function

#End Region

    End Class

End Namespace
