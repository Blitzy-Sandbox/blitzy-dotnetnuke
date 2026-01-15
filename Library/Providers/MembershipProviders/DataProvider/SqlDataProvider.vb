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
Imports System.Data
Imports System.Data.SqlClient

Imports Microsoft.ApplicationBlocks.Data

Imports DotNetNuke
Imports DotNetNuke.Common.Utilities
Imports DotNetNuke.Entities.Users


Namespace DotNetNuke.Security.Membership.Data

    ''' -----------------------------------------------------------------------------
    ''' Project:    DotNetNuke
    ''' Namespace:  DotNetNuke.Security.Membership
    ''' Class:      SqlDataProvider
    ''' -----------------------------------------------------------------------------
    ''' <summary>
    ''' The SqlDataProvider provides a concrete SQL Server implementation of the
    ''' Data Access Layer for the project
    ''' </summary>
    ''' <remarks>
    ''' </remarks>
    ''' <history>
    '''     [cnurse]	03/28/2006	created
    ''' </history>
    ''' -----------------------------------------------------------------------------
    Public Class SqlDataProvider
        Inherits DataProvider

#Region "Private Members"
        Private Const ProviderType As String = "data"
        Private _providerConfiguration As Framework.Providers.ProviderConfiguration = Framework.Providers.ProviderConfiguration.GetProviderConfiguration(ProviderType)
        Private _connectionString As String
        Private _providerPath As String
        Private _objectQualifier As String
        Private _databaseOwner As String
#End Region

#Region "Constructors"

        Public Sub New()

            ' Read the configuration specific information for this provider
            Dim objProvider As Framework.Providers.Provider = CType(_providerConfiguration.Providers(_providerConfiguration.DefaultProvider), Framework.Providers.Provider)

            ' Read the attributes for this provider
            'Get Connection string from web.config
            _connectionString = Config.GetConnectionString()

            If _connectionString = "" Then
                ' Use connection string specified in provider
                _connectionString = objProvider.Attributes("connectionString")
            End If

            _providerPath = objProvider.Attributes("providerPath")

            _objectQualifier = objProvider.Attributes("objectQualifier")
            If _objectQualifier <> "" And _objectQualifier.EndsWith("_") = False Then
                _objectQualifier += "_"
            End If

            _databaseOwner = objProvider.Attributes("databaseOwner")
            If _databaseOwner <> "" And _databaseOwner.EndsWith(".") = False Then
                _databaseOwner += "."
            End If

        End Sub

#End Region

#Region "Properties"

        Public ReadOnly Property ConnectionString() As String
            Get
                Return _connectionString
            End Get
        End Property

        Public ReadOnly Property ProviderPath() As String
            Get
                Return _providerPath
            End Get
        End Property

        Public ReadOnly Property ObjectQualifier() As String
            Get
                Return _objectQualifier
            End Get
        End Property

        Public ReadOnly Property DatabaseOwner() As String
            Get
                Return _databaseOwner
            End Get
        End Property

#End Region

#Region "General Public Methods"

        Private Function GetNull(ByVal Field As Object) As Object
            Return DotNetNuke.Common.Utilities.Null.GetNull(Field, DBNull.Value)
        End Function

        Private Function GetFullyQualifiedName(ByVal name As String) As String
            Return DatabaseOwner & ObjectQualifier & name
        End Function

#End Region

#Region "Public Methods"

        'Security
        Public Overrides Function UserLogin(ByVal Username As String, ByVal Password As String) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, GetFullyQualifiedName("UserLogin"), Username, Password), IDataReader)
        End Function

        Public Overrides Function GetAuthRoles(ByVal PortalId As Integer, ByVal ModuleId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, GetFullyQualifiedName("GetAuthRoles"), PortalId, ModuleId), IDataReader)
        End Function

        'Users
        Public Overrides Function AddUser(ByVal PortalID As Integer, ByVal Username As String, ByVal FirstName As String, ByVal LastName As String, ByVal AffiliateId As Integer, ByVal IsSuperUser As Boolean, ByVal Email As String, ByVal DisplayName As String, ByVal UpdatePassword As Boolean, ByVal IsApproved As Boolean) As Integer
            Try
                Return CType(SqlHelper.ExecuteScalar(ConnectionString, DatabaseOwner & ObjectQualifier & "AddUser", PortalID, Username, FirstName, LastName, GetNull(AffiliateId), IsSuperUser, Email, DisplayName, UpdatePassword, IsApproved), Integer)
            Catch    ' duplicate
                Return -1
            End Try
        End Function

        Public Overrides Sub DeleteUserPortal(ByVal UserId As Integer, ByVal PortalId As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, GetFullyQualifiedName("DeleteUserPortal"), UserId, PortalId)
        End Sub

        Public Overrides Sub DeleteUser(ByVal UserId As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, GetFullyQualifiedName("DeleteUser"), UserId)
        End Sub

        Public Overrides Sub UpdateUser(ByVal UserId As Integer, ByVal PortalID As Integer, ByVal FirstName As String, ByVal LastName As String, ByVal Email As String, ByVal DisplayName As String, ByVal UpdatePassword As Boolean, ByVal IsApproved As Boolean)
            SqlHelper.ExecuteNonQuery(ConnectionString, GetFullyQualifiedName("UpdateUser"), UserId, PortalID, FirstName, LastName, Email, DisplayName, UpdatePassword, IsApproved)
        End Sub

        Public Overrides Function GetAllUsers(ByVal PortalID As Integer, ByVal pageIndex As Integer, ByVal pageSize As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, GetFullyQualifiedName("GetAllUsers"), GetNull(PortalID), pageIndex, pageSize), IDataReader)
        End Function

        Public Overrides Function GetUnAuthorizedUsers(ByVal portalId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, GetFullyQualifiedName("GetUnAuthorizedUsers"), GetNull(portalId)), IDataReader)
        End Function

        Public Overrides Function GetUser(ByVal PortalId As Integer, ByVal UserId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, GetFullyQualifiedName("GetUser"), PortalId, UserId), IDataReader)
        End Function

        Public Overrides Function GetUserByAuthToken(ByVal PortalID As Integer, ByVal UserToken As String, ByVal AuthType As String) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, GetFullyQualifiedName("GetUserByAuthToken"), PortalID, UserToken, AuthType), IDataReader)
        End Function

        Public Overrides Function GetUserByUsername(ByVal PortalId As Integer, ByVal Username As String) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, GetFullyQualifiedName("GetUserByUsername"), GetNull(PortalId), Username), IDataReader)
        End Function

        Public Overrides Function GetUserCountByPortal(ByVal portalId As Integer) As Integer
            Return CType(SqlHelper.ExecuteScalar(ConnectionString, GetFullyQualifiedName("GetUserCountByPortal"), portalId), Integer)
        End Function

        Public Overrides Function GetUsersByEmail(ByVal PortalID As Integer, ByVal Email As String, ByVal pageIndex As Integer, ByVal pageSize As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, GetFullyQualifiedName("GetUsersByEmail"), GetNull(PortalID), Email, pageIndex, pageSize), IDataReader)
        End Function

        Public Overrides Function GetUsersByProfileProperty(ByVal PortalID As Integer, ByVal propertyName As String, ByVal propertyValue As String, ByVal pageIndex As Integer, ByVal pageSize As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, GetFullyQualifiedName("GetUsersByProfileProperty"), GetNull(PortalID), propertyName, propertyValue, pageIndex, pageSize), IDataReader)
        End Function

        Public Overrides Function GetUsersByRolename(ByVal PortalID As Integer, ByVal Rolename As String) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, GetFullyQualifiedName("GetUsersByRolename"), GetNull(PortalID), Rolename), IDataReader)
        End Function

        Public Overrides Function GetUsersByUsername(ByVal PortalID As Integer, ByVal Username As String, ByVal pageIndex As Integer, ByVal pageSize As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, GetFullyQualifiedName("GetUsersByUsername"), GetNull(PortalID), Username, pageIndex, pageSize), IDataReader)
        End Function

        Public Overrides Function GetSuperUsers() As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, GetFullyQualifiedName("GetSuperUsers")), IDataReader)
        End Function

        'Roles
        Public Overrides Function GetRolesByUser(ByVal UserId As Integer, ByVal PortalId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, GetFullyQualifiedName("GetRolesByUser"), UserId, PortalId), IDataReader)
        End Function

        Public Overrides Function GetPortalRoles(ByVal PortalId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, GetFullyQualifiedName("GetPortalRoles"), PortalId), IDataReader)
        End Function

        Public Overrides Function GetRoles() As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, GetFullyQualifiedName("GetRoles")), IDataReader)
        End Function

        Public Overrides Function GetRole(ByVal RoleId As Integer, ByVal PortalId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, GetFullyQualifiedName("GetRole"), RoleId, PortalId), IDataReader)
        End Function

        Public Overrides Function GetRoleByName(ByVal PortalId As Integer, ByVal RoleName As String) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, GetFullyQualifiedName("GetRoleByName"), PortalId, RoleName), IDataReader)
        End Function

        Public Overrides Function GetRolesByGroup(ByVal RoleGroupId As Integer, ByVal PortalId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, GetFullyQualifiedName("GetRolesByGroup"), GetNull(RoleGroupId), PortalId), IDataReader)
        End Function

        Public Overrides Function AddRole(ByVal PortalId As Integer, ByVal RoleGroupId As Integer, ByVal RoleName As String, ByVal Description As String, ByVal ServiceFee As Single, ByVal BillingPeriod As String, ByVal BillingFrequency As String, ByVal TrialFee As Single, ByVal TrialPeriod As Integer, ByVal TrialFrequency As String, ByVal IsPublic As Boolean, ByVal AutoAssignment As Boolean, ByVal RSVPCode As String, ByVal IconFile As String) As Integer
            Return CType(SqlHelper.ExecuteScalar(ConnectionString, GetFullyQualifiedName("AddRole"), PortalId, GetNull(RoleGroupId), RoleName, Description, ServiceFee, BillingPeriod, GetNull(BillingFrequency), TrialFee, TrialPeriod, GetNull(TrialFrequency), IsPublic, AutoAssignment, RSVPCode, IconFile), Integer)
        End Function

        Public Overrides Sub DeleteRole(ByVal RoleId As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, GetFullyQualifiedName("DeleteRole"), RoleId)
        End Sub

        Public Overrides Sub UpdateRole(ByVal RoleId As Integer, ByVal RoleGroupId As Integer, ByVal Description As String, ByVal ServiceFee As Single, ByVal BillingPeriod As String, ByVal BillingFrequency As String, ByVal TrialFee As Single, ByVal TrialPeriod As Integer, ByVal TrialFrequency As String, ByVal IsPublic As Boolean, ByVal AutoAssignment As Boolean, ByVal RSVPCode As String, ByVal IconFile As String)
            SqlHelper.ExecuteNonQuery(ConnectionString, GetFullyQualifiedName("UpdateRole"), RoleId, GetNull(RoleGroupId), Description, ServiceFee, BillingPeriod, GetNull(BillingFrequency), TrialFee, TrialPeriod, GetNull(TrialFrequency), IsPublic, AutoAssignment, RSVPCode, IconFile)
        End Sub

        'Role Groups
        Public Overrides Function AddRoleGroup(ByVal PortalId As Integer, ByVal GroupName As String, ByVal Description As String) As Integer
            Return CType(SqlHelper.ExecuteScalar(ConnectionString, GetFullyQualifiedName("AddRoleGroup"), PortalId, GroupName, Description), Integer)
        End Function

        Public Overrides Sub DeleteRoleGroup(ByVal RoleGroupId As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, GetFullyQualifiedName("DeleteRoleGroup"), RoleGroupId)
        End Sub

        Public Overrides Function GetRoleGroup(ByVal portalId As Integer, ByVal roleGroupId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, GetFullyQualifiedName("GetRoleGroup"), portalId, roleGroupId), IDataReader)
        End Function

        Public Overrides Function GetRoleGroups(ByVal portalId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, GetFullyQualifiedName("GetRoleGroups"), portalId), IDataReader)
        End Function

        Public Overrides Sub UpdateRoleGroup(ByVal RoleGroupId As Integer, ByVal GroupName As String, ByVal Description As String)
            SqlHelper.ExecuteNonQuery(ConnectionString, GetFullyQualifiedName("UpdateRoleGroup"), RoleGroupId, GroupName, Description)
        End Sub

        'User Roles
        Public Overrides Function GetUserRole(ByVal PortalID As Integer, ByVal UserId As Integer, ByVal RoleId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, GetFullyQualifiedName("GetUserRole"), PortalID, UserId, RoleId), IDataReader)
        End Function

        Public Overrides Function GetUserRoles(ByVal PortalID As Integer, ByVal UserId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, GetFullyQualifiedName("GetUserRoles"), PortalID, UserId), IDataReader)
        End Function

        Public Overrides Function GetUserRolesByUsername(ByVal PortalID As Integer, ByVal Username As String, ByVal Rolename As String) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, GetFullyQualifiedName("GetUserRolesByUsername"), PortalID, GetNull(Username), GetNull(Rolename)), IDataReader)
        End Function

        Public Overrides Function AddUserRole(ByVal PortalId As Integer, ByVal UserId As Integer, ByVal RoleId As Integer, ByVal EffectiveDate As Date, ByVal ExpiryDate As Date) As Integer
            Return CType(SqlHelper.ExecuteScalar(ConnectionString, GetFullyQualifiedName("AddUserRole"), PortalId, UserId, RoleId, GetNull(EffectiveDate), GetNull(ExpiryDate)), Integer)
        End Function

        Public Overrides Sub UpdateUserRole(ByVal UserRoleId As Integer, ByVal EffectiveDate As Date, ByVal ExpiryDate As Date)
            SqlHelper.ExecuteNonQuery(ConnectionString, GetFullyQualifiedName("UpdateUserRole"), UserRoleId, GetNull(EffectiveDate), GetNull(ExpiryDate))
        End Sub

        Public Overrides Sub DeleteUserRole(ByVal UserId As Integer, ByVal RoleId As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, GetFullyQualifiedName("DeleteUserRole"), UserId, RoleId)
        End Sub

        Public Overrides Function GetServices(ByVal PortalId As Integer, ByVal UserId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, GetFullyQualifiedName("GetServices"), PortalId, GetNull(UserId)), IDataReader)
        End Function

        Public Overrides Function GetUsers(ByVal PortalId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, GetFullyQualifiedName("GetUsers"), GetNull(PortalId)), IDataReader)
        End Function

        'Profile
        Public Overrides Function GetUserProfile(ByVal UserId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, GetFullyQualifiedName("GetUserProfile"), UserId), IDataReader)
        End Function

        Public Overrides Sub UpdateProfileProperty(ByVal ProfileId As Integer, ByVal UserId As Integer, ByVal PropertyDefinitionID As Integer, ByVal PropertyValue As String, ByVal Visibility As Integer, ByVal LastUpdatedDate As DateTime)
            SqlHelper.ExecuteNonQuery(ConnectionString, GetFullyQualifiedName("UpdateUserProfileProperty"), GetNull(ProfileId), UserId, PropertyDefinitionID, PropertyValue, Visibility, LastUpdatedDate)
        End Sub

        ' users online
        Public Overrides Sub DeleteUsersOnline(ByVal TimeWindow As Integer)
            SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "DeleteUsersOnline", TimeWindow)
        End Sub

        Public Overrides Function GetOnlineUser(ByVal UserId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetOnlineUser", UserId), IDataReader)
        End Function

        Public Overrides Function GetOnlineUsers(ByVal PortalId As Integer) As IDataReader
            Return CType(SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & "GetOnlineUsers", PortalId), IDataReader)
        End Function

        Public Overrides Sub UpdateUsersOnline(ByVal UserList As Hashtable)

            If (UserList.Count = 0) Then
                'No users to process, quit method
                Return
            End If
            For Each key As String In UserList.Keys
                If TypeOf UserList(key) Is AnonymousUserInfo Then
                    Dim user As AnonymousUserInfo = CType(UserList(key), AnonymousUserInfo)
                    SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "UpdateAnonymousUser", user.UserID, user.PortalID, user.TabID, user.LastActiveDate)
                ElseIf TypeOf UserList(key) Is OnlineUserInfo Then
                    Dim user As OnlineUserInfo = CType(UserList(key), OnlineUserInfo)
                    SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & "UpdateOnlineUser", user.UserID, user.PortalID, user.TabID, user.LastActiveDate)
                End If
            Next

        End Sub

#End Region

    End Class

End Namespace