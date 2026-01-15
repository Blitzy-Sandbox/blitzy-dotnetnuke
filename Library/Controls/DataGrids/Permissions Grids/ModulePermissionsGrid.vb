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
Imports System.Drawing
Imports System.Text
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports System.IO
Imports System.Collections
Imports System.Data
Imports DotNetNuke.Security.Roles
Imports DotNetNuke.Services.Localization

Namespace DotNetNuke.Security.Permissions.Controls

    Public Class ModulePermissionsGrid
        Inherits PermissionsGrid

#Region "Private Members"

        Private _InheritViewPermissionsFromTab As Boolean = False
        Private _ModuleID As Integer = -1
        Private _TabId As Integer = -1
        Private ModulePermissions As ModulePermissionCollection
        Private _ViewColumnIndex As Integer

#End Region

#Region "Public Properties"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets and Sets whether the Module inherits the Page's(Tab's) permissions
        ''' </summary>
        ''' <history>
        '''     [cnurse]    01/09/2006  Documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Property InheritViewPermissionsFromTab() As Boolean
            Get
                Return _InheritViewPermissionsFromTab
            End Get
            Set(ByVal Value As Boolean)
                _InheritViewPermissionsFromTab = Value
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets and Sets the Id of the Module
        ''' </summary>
        ''' <history>
        '''     [cnurse]    01/09/2006  Documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Property ModuleID() As Integer
            Get
                Return _ModuleID
            End Get
            Set(ByVal Value As Integer)
                _ModuleID = Value
                If Not Page.IsPostBack Then
                    GetModulePermissions()
                End If
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets and Sets the Id of the Tab associated with this module
        ''' </summary>
        ''' <history>
        '''     [cnurse]    24/11/2006  Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Property TabId() As Integer
            Get
                Return _TabId
            End Get
            Set(ByVal Value As Integer)
                _TabId = Value
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the ModulePermission Collection
        ''' </summary>
        ''' <history>
        '''     [cnurse]    01/09/2006  Documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public ReadOnly Property Permissions() As Security.Permissions.ModulePermissionCollection
            Get
                'First Update Permissions in case they have been changed
                UpdatePermissions()

                'Return the ModulePermissions
                Return ModulePermissions
            End Get
        End Property

#End Region

#Region "Private Methods"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the ModulePermissions from the Data Store
        ''' </summary>
        ''' <history>
        '''     [cnurse]    01/12/2006  Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub GetModulePermissions()

            Dim objModulePermissionController As New Security.Permissions.ModulePermissionController
            ModulePermissions = objModulePermissionController.GetModulePermissionsCollectionByModuleID(Me.ModuleID, Me.TabId)

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Parse the Permission Keys used to persist the Permissions in the ViewState
        ''' </summary>
        ''' <param name="Settings">A string array of settings</param>
        ''' <param name="arrPermisions">An Arraylist to add the Permission object to</param>
        ''' <history>
        '''     [cnurse]    01/09/2006  Documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub ParsePermissionKeys(ByVal Settings As String(), ByVal arrPermisions As ArrayList)

            Dim objRoleController As New RoleController
            Dim objModulePermissionController As New Security.Permissions.ModulePermissionController
            Dim objModulePermission As Security.Permissions.ModulePermissionInfo

            'Get the permission
            Dim permissionID As Integer = Convert.ToInt32(Settings(1))
            Dim objPermissionController As New Security.Permissions.PermissionController
            Dim objPermission As PermissionInfo = objPermissionController.GetPermission(permissionID)

            objModulePermission = New Security.Permissions.ModulePermissionInfo(objPermission)

            objModulePermission.RoleID = Convert.ToInt32(Settings(4))

            If Settings(2) = "" Then
                objModulePermission.ModulePermissionID = -1
            Else
                objModulePermission.ModulePermissionID = Convert.ToInt32(Settings(2))
            End If
            objModulePermission.RoleName = Settings(3)
            objModulePermission.AllowAccess = True
            objModulePermission.UserID = Convert.ToInt32(Settings(5))
            objModulePermission.DisplayName = Settings(6)

            objModulePermission.ModuleID = ModuleID
            arrPermisions.Add(objModulePermission)

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Check if the Role has the permission specified
        ''' </summary>
        ''' <param name="permissionID">The Id of the Permission to check</param>
        ''' <param name="roleId">The role id to check</param>
        ''' <history>
        '''     [cnurse]    01/09/2006  Documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Function ModuleHasRolePermission(ByVal permissionID As Integer, ByVal roleid As Integer) As Security.Permissions.ModulePermissionInfo
            Dim i As Integer
            For i = 0 To ModulePermissions.Count - 1
                Dim objModulePermission As Security.Permissions.ModulePermissionInfo = ModulePermissions(i)
                If permissionID = objModulePermission.PermissionID And objModulePermission.RoleID = roleid Then
                    Return objModulePermission
                End If
            Next
            Return Nothing
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Check if the Role has the permission specified
        ''' </summary>
        ''' <param name="permissionID">The Id of the Permission to check</param>
        ''' <param name="userId">The user id to check</param>
        ''' <history>
        '''     [cnurse]    01/09/2006  Documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Function ModuleHasUserPermission(ByVal permissionID As Integer, ByVal userid As Integer) As Security.Permissions.ModulePermissionInfo
            Dim i As Integer
            For i = 0 To ModulePermissions.Count - 1
                Dim objModulePermission As Security.Permissions.ModulePermissionInfo = ModulePermissions(i)
                If permissionID = objModulePermission.PermissionID And objModulePermission.UserID = userid Then
                    Return objModulePermission
                End If
            Next
            Return Nothing
        End Function

#End Region

#Region "Protected Methods"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the Enabled status of the permission
        ''' </summary>
        ''' <param name="objPerm">The permission being loaded</param>
        ''' <param name="role">The role</param>
        ''' <param name="column">The column of the Grid</param>
        ''' <history>
        '''     [cnurse]    01/13/2006  Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Protected Overrides Function GetEnabled(ByVal objPerm As PermissionInfo, ByVal role As RoleInfo, ByVal column As Integer) As Boolean

            Dim enabled As Boolean

            If InheritViewPermissionsFromTab And column = _ViewColumnIndex Then
                enabled = False
            Else
                If role.RoleID = AdministratorRoleId Then
                    enabled = False
                Else
                    enabled = True
                End If
            End If

            Return enabled

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the Enabled status of the permission
        ''' </summary>
        ''' <param name="objPerm">The permission being loaded</param>
        ''' <param name="user">The user</param>
        ''' <param name="column">The column of the Grid</param>
        ''' <history>
        '''     [cnurse]    01/13/2006  Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Protected Overrides Function GetEnabled(ByVal objPerm As PermissionInfo, ByVal user As UserInfo, ByVal column As Integer) As Boolean

            Dim enabled As Boolean

            If InheritViewPermissionsFromTab And column = _ViewColumnIndex Then
                enabled = False
            Else
                enabled = True
            End If

            Return enabled

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the Value of the permission
        ''' </summary>
        ''' <param name="objPerm">The permission being loaded</param>
        ''' <param name="role">The role</param>
        ''' <param name="column">The column of the Grid</param>
        ''' <returns>A Boolean (True or False)</returns>
        ''' <history>
        '''     [cnurse]    01/09/2006  Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Protected Overrides Function GetPermission(ByVal objPerm As PermissionInfo, ByVal role As RoleInfo, ByVal column As Integer) As Boolean

            Dim permission As Boolean

            If InheritViewPermissionsFromTab And column = _ViewColumnIndex Then
                permission = False
            Else
                If role.RoleID = AdministratorRoleId Then
                    permission = True
                Else
                    Dim objModulePermission As Security.Permissions.ModulePermissionInfo = ModuleHasRolePermission(objPerm.PermissionID, role.RoleID)
                    If Not objModulePermission Is Nothing Then
                        permission = objModulePermission.AllowAccess
                    Else
                        permission = False
                    End If
                End If
            End If

            Return permission

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the Value of the permission
        ''' </summary>
        ''' <param name="objPerm">The permission being loaded</param>
        ''' <param name="user">The role</param>
        ''' <param name="column">The column of the Grid</param>
        ''' <returns>A Boolean (True or False)</returns>
        ''' <history>
        '''     [cnurse]    01/09/2006  Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Protected Overrides Function GetPermission(ByVal objPerm As PermissionInfo, ByVal user As UserInfo, ByVal column As Integer) As Boolean

            Dim permission As Boolean

            If InheritViewPermissionsFromTab And column = _ViewColumnIndex Then
                permission = False
            Else
                Dim objModulePermission As Security.Permissions.ModulePermissionInfo = ModuleHasUserPermission(objPerm.PermissionID, user.UserID)
                If Not objModulePermission Is Nothing Then
                    permission = objModulePermission.AllowAccess
                Else
                    permission = False
                End If
            End If

            Return permission

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the Permissions from the Data Store
        ''' </summary>
        ''' <history>
        '''     [cnurse]    01/09/2006  Documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Protected Overrides Function GetPermissions() As ArrayList

            Dim objPermissionController As New Security.Permissions.PermissionController
            Dim arrPermissions As ArrayList = objPermissionController.GetPermissionsByModuleID(Me.ModuleID)

            Dim i As Integer
            For i = 0 To arrPermissions.Count - 1
                Dim objPermission As Security.Permissions.PermissionInfo
                objPermission = CType(arrPermissions(i), Security.Permissions.PermissionInfo)
                If objPermission.PermissionKey = "VIEW" Then
                    _ViewColumnIndex = i + 1
                End If
            Next

            Return arrPermissions

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the users from the Database
        ''' </summary>
        ''' <history>
        '''     [cnurse]    01/12/2006  Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Protected Overrides Function GetUsers() As ArrayList

            Dim arrUsers As New ArrayList
            Dim objModulePermission As Security.Permissions.ModulePermissionInfo
            Dim objUser As UserInfo
            Dim blnExists As Boolean

            For Each objModulePermission In ModulePermissions
                If Not Null.IsNull(objModulePermission.UserID) Then
                    blnExists = False
                    For Each objUser In arrUsers
                        If objModulePermission.UserID = objUser.UserID Then
                            blnExists = True
                        End If
                    Next
                    If Not blnExists Then
                        objUser = New UserInfo
                        objUser.UserID = objModulePermission.UserID
                        objUser.Username = objModulePermission.Username
                        objUser.DisplayName = objModulePermission.DisplayName
                        arrUsers.Add(objUser)
                    End If
                End If
            Next
            Return arrUsers

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Load the ViewState
        ''' </summary>
        ''' <param name="savedState">The saved state</param>
        ''' <history>
        '''     [cnurse]    01/12/2006  Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Protected Overrides Sub LoadViewState(ByVal savedState As Object)

            If Not (savedState Is Nothing) Then
                ' Load State from the array of objects that was saved with SaveViewState.

                Dim myState As Object() = CType(savedState, Object())

                'Load Base Controls ViewState
                If Not (myState(0) Is Nothing) Then
                    MyBase.LoadViewState(myState(0))
                End If

                'Load ModuleID
                If Not (myState(1) Is Nothing) Then
                    ModuleID = CInt(myState(1))
                End If

                'Load InheritViewPermissionsFromTab
                If Not (myState(2) Is Nothing) Then
                    InheritViewPermissionsFromTab = CBool(myState(2))
                End If

                'Load ModulePermissions
                If Not (myState(3) Is Nothing) Then
                    Dim arrPermissions As New ArrayList
                    Dim state As String = CStr(myState(3))
                    If state <> "" Then
                        'First Break the String into individual Keys
                        Dim permissionKeys As String() = Split(state, "##")
                        For Each key As String In permissionKeys
                            Dim Settings As String() = Split(key, "|")
                            ParsePermissionKeys(Settings, arrPermissions)
                        Next
                    End If
                    ModulePermissions = New ModulePermissionCollection(arrPermissions)
                End If
            End If

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Saves the ViewState
        ''' </summary>
        ''' <history>
        '''     [cnurse]    01/12/2006  Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Protected Overrides Function SaveViewState() As Object

            Dim allStates(3) As Object

            ' Save the Base Controls ViewState
            allStates(0) = MyBase.SaveViewState()

            'Save the ModuleID
            allStates(1) = ModuleID

            'Save the InheritViewPermissionsFromTab
            allStates(2) = InheritViewPermissionsFromTab

            'Persist the ModulePermissions
            Dim sb As New StringBuilder
            Dim addDelimiter As Boolean = False
            For Each objModulePermission As ModulePermissionInfo In ModulePermissions
                If addDelimiter Then
                    sb.Append("##")
                Else
                    addDelimiter = True
                End If
                sb.Append(BuildKey(objModulePermission.AllowAccess, objModulePermission.PermissionID, objModulePermission.ModulePermissionID, objModulePermission.RoleID, objModulePermission.RoleName, objModulePermission.UserID, objModulePermission.DisplayName))
            Next
            allStates(3) = sb.ToString()

            Return allStates

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Updates a Permission
        ''' </summary>
        ''' <param name="permission">The permission being updated</param>
        ''' <param name="roleName">The name of the role</param>
        ''' <param name="roleId">The id of the role</param>
        ''' <param name="allowAccess">The value of the permission</param>
        ''' <history>
        '''     [cnurse]    01/12/2006  Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Protected Overrides Sub UpdatePermission(ByVal permission As PermissionInfo, ByVal roleid As Integer, ByVal roleName As String, ByVal allowAccess As Boolean)

            Dim isMatch As Boolean = False
            Dim objPermission As ModulePermissionInfo
            Dim permissionId As Integer = permission.PermissionID

            'Search ModulePermission Collection for the permission to Update
            For Each objPermission In ModulePermissions
                If objPermission.PermissionID = permissionId And objPermission.RoleID = roleid Then
                    'ModulePermission is in collection
                    If Not allowAccess Then
                        'Remove from collection as we only keep AllowAccess permissions
                        ModulePermissions.Remove(objPermission)
                    End If
                    isMatch = True
                    Exit For
                End If
            Next

            'ModulePermission not found so add new
            If Not isMatch And allowAccess Then
                objPermission = New ModulePermissionInfo
                objPermission.PermissionID = permissionId
                objPermission.ModuleID = ModuleID
                objPermission.RoleID = roleid
                objPermission.RoleName = roleName
                objPermission.AllowAccess = allowAccess
                objPermission.UserID = Null.NullInteger
                objPermission.DisplayName = Null.NullString
                ModulePermissions.Add(objPermission)
            End If

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Updates a Permission
        ''' </summary>
        ''' <param name="permission">The permission being updated</param>
        ''' <param name="displayName">The user's displayname</param>
        ''' <param name="userId">The user's id</param>
        ''' <param name="allowAccess">The value of the permission</param>
        ''' <history>
        '''     [cnurse]    01/12/2006  Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Protected Overrides Sub UpdatePermission(ByVal permission As PermissionInfo, ByVal displayName As String, ByVal userId As Integer, ByVal allowAccess As Boolean)

            Dim isMatch As Boolean = False
            Dim objPermission As ModulePermissionInfo
            Dim permissionId As Integer = permission.PermissionID

            'Search ModulePermission Collection for the permission to Update
            For Each objPermission In ModulePermissions
                If objPermission.PermissionID = permissionId And objPermission.UserID = userId Then
                    'TabPermission is in collection
                    If Not allowAccess Then
                        'Remove from collection as we only keep AllowAccess permissions
                        ModulePermissions.Remove(objPermission)
                    End If
                    isMatch = True
                    Exit For
                End If
            Next

            'ModulePermission not found so add new
            If Not isMatch And allowAccess Then
                objPermission = New ModulePermissionInfo
                objPermission.PermissionID = permissionId
                objPermission.ModuleID = ModuleID
                objPermission.RoleID = Integer.Parse(glbRoleNothing)
                objPermission.RoleName = Null.NullString
                objPermission.AllowAccess = allowAccess
                objPermission.UserID = userId
                objPermission.DisplayName = displayName
                ModulePermissions.Add(objPermission)
            End If

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Updates a Permission
        ''' </summary>
        ''' <param name="permissions">The permissions collection</param>
        ''' <param name="user">The user to add</param>
        ''' <history>
        '''     [cnurse]    01/12/2006  Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Protected Overrides Sub AddPermission(ByVal permissions As ArrayList, ByVal user As UserInfo)

            Dim objModulePermission As ModulePermissionInfo

            'Search TabPermission Collection for the user 
            Dim isMatch As Boolean = False
            For Each objModulePermission In ModulePermissions
                If objModulePermission.UserID = user.UserID Then
                    isMatch = True
                    Exit For
                End If
            Next

            'user not found so add new
            If Not isMatch Then
                Dim objPermission As Security.Permissions.PermissionInfo
                For Each objPermission In permissions
                    objModulePermission = New ModulePermissionInfo
                    objModulePermission.PermissionID = objPermission.PermissionID
                    objModulePermission.ModuleID = ModuleID
                    objModulePermission.RoleID = Integer.Parse(glbRoleNothing)
                    objModulePermission.RoleName = Null.NullString
                    If objPermission.PermissionKey = "VIEW" Then
                        objModulePermission.AllowAccess = True
                    Else
                        objModulePermission.AllowAccess = False
                    End If
                    objModulePermission.UserID = user.UserID
                    objModulePermission.DisplayName = user.DisplayName
                    ModulePermissions.Add(objModulePermission)
                Next
            End If

        End Sub

#End Region

#Region "Public Methods"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Overrides the Base method to Generate the Data Grid
        ''' </summary>
        ''' <history>
        '''     [cnurse]    01/09/2006  Documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Overrides Sub GenerateDataGrid()

        End Sub

#End Region

    End Class

End Namespace