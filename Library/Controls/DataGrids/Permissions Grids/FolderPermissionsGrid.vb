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

    Public Class FolderPermissionsGrid
        Inherits PermissionsGrid

#Region "Private Members"

        Private _FolderPath As String = ""
        Private FolderPermissions As FolderPermissionCollection

#End Region

#Region "Public Properties"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets and Sets the path of the Folder
        ''' </summary>
        ''' <history>
        '''     [cnurse]    01/09/2006  Documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Property FolderPath() As String
            Get
                Return _FolderPath
            End Get
            Set(ByVal Value As String)
                _FolderPath = Value
                GetFolderPermissions()
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the Permission Collection
        ''' </summary>
        ''' <history>
        '''     [cnurse]    01/09/2006  Documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public ReadOnly Property Permissions() As Security.Permissions.FolderPermissionCollection
            Get
                'First Update Permissions in case they have been changed
                UpdatePermissions()

                'Return the FolderPermissions
                Return FolderPermissions
            End Get
        End Property

#End Region

#Region "Private Methods"

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
        Private Function FolderHasPermission(ByVal permissionID As Integer, ByVal roleid As Integer) As Security.Permissions.FolderPermissionInfo
            Dim i As Integer
            For i = 0 To FolderPermissions.Count - 1
                Dim objFolderPermission As Security.Permissions.FolderPermissionInfo = FolderPermissions(i)
                If objFolderPermission.RoleID = roleid And permissionID = objFolderPermission.PermissionID Then
                    Return objFolderPermission
                End If
            Next
            Return Nothing
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the TabPermissions from the Data Store
        ''' </summary>
        ''' <history>
        '''     [cnurse]    01/12/2006  Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub GetFolderPermissions()

            Dim objFolderPermissionController As New Security.Permissions.FolderPermissionController
            FolderPermissions = objFolderPermissionController.GetFolderPermissionsCollectionByFolderPath(PortalId, Me.FolderPath)

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

            Dim _portalSettings As PortalSettings = PortalController.GetCurrentPortalSettings
            Dim objFolderPermission As Security.Permissions.FolderPermissionInfo

            objFolderPermission = New Security.Permissions.FolderPermissionInfo
            objFolderPermission.PermissionID = Convert.ToInt32(Settings(1))
            objFolderPermission.RoleID = Convert.ToInt32(Settings(4))
            objFolderPermission.RoleName = Settings(3)
            objFolderPermission.AllowAccess = True
            objFolderPermission.UserID = Convert.ToInt32(Settings(5))
            objFolderPermission.DisplayName = Settings(6)

            If Settings(2) = "" Then
                objFolderPermission.FolderPermissionID = -1
            Else
                objFolderPermission.FolderPermissionID = Convert.ToInt32(Settings(2))
            End If
            objFolderPermission.FolderPath = FolderPath

            'Add FolderPermission to array
            arrPermisions.Add(objFolderPermission)

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
        Private Function FolderHasRolePermission(ByVal permissionID As Integer, ByVal roleid As Integer) As Security.Permissions.FolderPermissionInfo
            Dim i As Integer
            For i = 0 To FolderPermissions.Count - 1
                Dim objFolderPermission As Security.Permissions.FolderPermissionInfo = FolderPermissions(i)
                If permissionID = objFolderPermission.PermissionID And objFolderPermission.RoleID = roleid Then
                    Return objFolderPermission
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
        Private Function FolderHasUserPermission(ByVal permissionID As Integer, ByVal userid As Integer) As Security.Permissions.FolderPermissionInfo
            Dim i As Integer
            For i = 0 To FolderPermissions.Count - 1
                Dim objFolderPermission As Security.Permissions.FolderPermissionInfo = FolderPermissions(i)
                If permissionID = objFolderPermission.PermissionID And objFolderPermission.UserID = userid Then
                    Return objFolderPermission
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

            If role.RoleID = AdministratorRoleId Then
                enabled = False
            Else
                enabled = True
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

            enabled = True

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

            If role.RoleID = AdministratorRoleId Then
                permission = True
            Else
                Dim objFolderPermission As Security.Permissions.FolderPermissionInfo = FolderHasRolePermission(objPerm.PermissionID, role.RoleID)
                If Not objFolderPermission Is Nothing Then
                    permission = objFolderPermission.AllowAccess
                Else
                    permission = False
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

            Dim objFolderPermission As Security.Permissions.FolderPermissionInfo = FolderHasUserPermission(objPerm.PermissionID, user.UserID)
            If Not objFolderPermission Is Nothing Then
                permission = objFolderPermission.AllowAccess
            Else
                permission = False
            End If

            Return permission

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the permissions from the Database
        ''' </summary>
        ''' <history>
        '''     [cnurse]    01/12/2006  Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Protected Overrides Function GetPermissions() As ArrayList

            Dim objPermissionController As New Security.Permissions.PermissionController
            Return objPermissionController.GetPermissionsByFolder(PortalId, Me.FolderPath)

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
            Dim objFolderPermission As Security.Permissions.FolderPermissionInfo
            Dim objUser As UserInfo
            Dim blnExists As Boolean

            For Each objFolderPermission In FolderPermissions
                If Not Null.IsNull(objFolderPermission.UserID) Then
                    blnExists = False
                    For Each objUser In arrUsers
                        If objFolderPermission.UserID = objUser.UserID Then
                            blnExists = True
                        End If
                    Next
                    If Not blnExists Then
                        objUser = New UserInfo
                        objUser.UserID = objFolderPermission.UserID
                        objUser.Username = objFolderPermission.Username
                        objUser.DisplayName = objFolderPermission.DisplayName
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

                'Load Base Controls ViewStte
                If Not (myState(0) Is Nothing) Then
                    MyBase.LoadViewState(myState(0))
                End If

                'Load FolderPath
                If Not (myState(1) Is Nothing) Then
                    FolderPath = CStr(myState(1))
                End If

                'Load FolderPermissions
                If Not (myState(2) Is Nothing) Then
                    Dim arrPermissions As New ArrayList
                    Dim state As String = CStr(myState(2))
                    If state <> "" Then
                        'First Break the String into individual Keys
                        Dim permissionKeys As String() = Split(state, "##")
                        For Each key As String In permissionKeys
                            Dim Settings As String() = Split(key, "|")
                            ParsePermissionKeys(Settings, arrPermissions)
                        Next
                    End If
                    FolderPermissions = New FolderPermissionCollection(arrPermissions)
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

            Dim allStates(2) As Object

            ' Save the Base Controls ViewState
            allStates(0) = MyBase.SaveViewState()

            'Save the Tab Id
            allStates(1) = FolderPath

            'Persist the TabPermisisons
            Dim sb As New StringBuilder
            Dim addDelimiter As Boolean = False
            For Each objFolderPermission As FolderPermissionInfo In FolderPermissions
                If addDelimiter Then
                    sb.Append("##")
                Else
                    addDelimiter = True
                End If
                sb.Append(BuildKey(objFolderPermission.AllowAccess, objFolderPermission.PermissionID, objFolderPermission.FolderPermissionID, objFolderPermission.RoleID, objFolderPermission.RoleName, objFolderPermission.UserID, objFolderPermission.DisplayName))
            Next
            allStates(2) = sb.ToString()

            Return allStates

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Updates a Permission
        ''' </summary>
        ''' <param name="permission">The permission being updated</param>
        ''' <param name="roleId">The id of the role</param>
        ''' <param name="roleName">The name of the role</param>
        ''' <param name="allowAccess">The value of the permission</param>
        ''' <history>
        '''     [cnurse]    01/12/2006  Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Protected Overrides Sub UpdatePermission(ByVal permission As PermissionInfo, ByVal roleid As Integer, ByVal roleName As String, ByVal allowAccess As Boolean)

            Dim isMatch As Boolean = False
            Dim objPermission As FolderPermissionInfo
            Dim permissionId As Integer = permission.PermissionID

            'Search FolderPermission Collection for the permission to Update
            For Each objPermission In FolderPermissions
                If objPermission.PermissionID = permissionId And objPermission.RoleID = roleid Then
                    'FolderPermission is in collection
                    If Not allowAccess Then
                        'Remove from collection as we only keep AllowAccess permissions
                        FolderPermissions.Remove(objPermission)
                    End If
                    isMatch = True
                    Exit For
                End If
            Next

            'FolderPermission not found so add new
            If Not isMatch And allowAccess Then
                objPermission = New FolderPermissionInfo
                objPermission.PermissionID = permissionId
                objPermission.FolderPath = FolderPath
                objPermission.RoleID = roleid
                objPermission.RoleName = roleName
                objPermission.AllowAccess = allowAccess
                objPermission.UserID = Null.NullInteger
                objPermission.DisplayName = Null.NullString
                FolderPermissions.Add(objPermission)
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
            Dim objPermission As FolderPermissionInfo
            Dim permissionId As Integer = permission.PermissionID

            'Search FolderPermission Collection for the permission to Update
            For Each objPermission In FolderPermissions
                If objPermission.PermissionID = permissionId And objPermission.UserID = userId Then
                    'TabPermission is in collection
                    If Not allowAccess Then
                        'Remove from collection as we only keep AllowAccess permissions
                        FolderPermissions.Remove(objPermission)
                    End If
                    isMatch = True
                    Exit For
                End If
            Next

            'FolderPermission not found so add new
            If Not isMatch And allowAccess Then
                objPermission = New FolderPermissionInfo
                objPermission.PermissionID = permissionId
                objPermission.FolderPath = FolderPath
                objPermission.RoleID = Integer.Parse(glbRoleNothing)
                objPermission.RoleName = Null.NullString
                objPermission.AllowAccess = allowAccess
                objPermission.UserID = userId
                objPermission.DisplayName = displayName
                FolderPermissions.Add(objPermission)
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

            Dim objFolderPermission As FolderPermissionInfo

            'Search TabPermission Collection for the user 
            Dim isMatch As Boolean = False
            For Each objFolderPermission In FolderPermissions
                If objFolderPermission.UserID = user.UserID Then
                    isMatch = True
                    Exit For
                End If
            Next

            'user not found so add new
            If Not isMatch Then
                Dim objPermission As Security.Permissions.PermissionInfo
                For Each objPermission In permissions
                    objFolderPermission = New FolderPermissionInfo
                    objFolderPermission.PermissionID = objPermission.PermissionID
                    objFolderPermission.FolderPath = FolderPath
                    objFolderPermission.RoleID = Integer.Parse(glbRoleNothing)
                    objFolderPermission.RoleName = Null.NullString
                    If objPermission.PermissionKey = "VIEW" Then
                        objFolderPermission.AllowAccess = True
                    Else
                        objFolderPermission.AllowAccess = False
                    End If
                    objFolderPermission.UserID = user.UserID
                    objFolderPermission.DisplayName = user.DisplayName
                    FolderPermissions.Add(objFolderPermission)
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