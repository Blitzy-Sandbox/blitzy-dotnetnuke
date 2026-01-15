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
Imports System.Web

Imports DotNetNuke.Common
Imports DotNetNuke.Common.Utilities
Imports DotNetNuke.Entities.Portals
Imports DotNetNuke.Entities.Users
Imports DotNetNuke.Framework.Providers
Imports DotNetNuke.Security.Membership.Data
Imports DotNetNuke.Security.Roles
Imports DotNetNuke.Services.Exceptions

Namespace DotNetNuke.Security.Membership

    ''' -----------------------------------------------------------------------------
    ''' Project:    DotNetNuke
    ''' Namespace:  DotNetNuke.Security.Membership
    ''' Class:      DNNRoleProvider
    ''' -----------------------------------------------------------------------------
    ''' <summary>
    ''' The DNNRoleProvider overrides the default MembershipProvider to provide
    ''' a purely DNN Membership Component implementation
    ''' </summary>
    ''' <remarks>
    ''' </remarks>
    ''' <history>
    '''     [cnurse]	03/28/2006	created
    ''' </history>
    ''' -----------------------------------------------------------------------------
    Public Class DNNRoleProvider
        Inherits RoleProvider

#Region "Private Shared Members"

        Private Shared dataProvider As DotNetNuke.Security.Membership.Data.DataProvider = DotNetNuke.Security.Membership.Data.DataProvider.Instance()

#End Region

        Private Function FillRoleInfo(ByVal dr As IDataReader) As RoleInfo
            Return FillRoleInfo(dr, True)
        End Function

        Private Function FillRoleInfo(ByVal dr As IDataReader, ByVal CheckForOpenDataReader As Boolean) As RoleInfo
            Dim objRoleInfo As RoleInfo = Nothing

            ' read datareader
            Dim canContinue As Boolean = True
            If CheckForOpenDataReader Then
                canContinue = False
                If dr.Read Then
                    canContinue = True
                End If
            End If
            If canContinue Then
                objRoleInfo = New RoleInfo
                objRoleInfo.RoleID = Convert.ToInt32(Null.SetNull(dr("RoleId"), objRoleInfo.RoleID))
                objRoleInfo.PortalID = Convert.ToInt32(Null.SetNull(dr("PortalID"), objRoleInfo.PortalID))
                objRoleInfo.RoleGroupID = Convert.ToInt32(Null.SetNull(dr("RoleGroupId"), objRoleInfo.RoleGroupID))
                objRoleInfo.RoleName = Convert.ToString(Null.SetNull(dr("RoleName"), objRoleInfo.RoleName))
                objRoleInfo.Description = Convert.ToString(Null.SetNull(dr("Description"), objRoleInfo.Description))
                objRoleInfo.ServiceFee = Convert.ToSingle(Null.SetNull(dr("ServiceFee"), objRoleInfo.ServiceFee))
                objRoleInfo.BillingPeriod = Convert.ToInt32(Null.SetNull(dr("BillingPeriod"), objRoleInfo.BillingPeriod))
                objRoleInfo.BillingFrequency = Convert.ToString(Null.SetNull(dr("BillingFrequency"), objRoleInfo.BillingFrequency))
                objRoleInfo.TrialFee = Convert.ToSingle(Null.SetNull(dr("TrialFee"), objRoleInfo.TrialFee))
                objRoleInfo.TrialPeriod = Convert.ToInt32(Null.SetNull(dr("TrialPeriod"), objRoleInfo.TrialPeriod))
                objRoleInfo.TrialFrequency = Convert.ToString(Null.SetNull(dr("TrialFrequency"), objRoleInfo.TrialFrequency))
                objRoleInfo.IsPublic = Convert.ToBoolean(Null.SetNull(dr("IsPublic"), objRoleInfo.IsPublic))
                objRoleInfo.AutoAssignment = Convert.ToBoolean(Null.SetNull(dr("AutoAssignment"), objRoleInfo.AutoAssignment))
                objRoleInfo.RSVPCode = Convert.ToString(Null.SetNull(dr("RSVPCode"), objRoleInfo.RSVPCode))
                objRoleInfo.IconFile = Convert.ToString(Null.SetNull(dr("IconFile"), objRoleInfo.IconFile))
            End If

            Return objRoleInfo
        End Function

        Private Function FillRoleInfoCollection(ByVal dr As IDataReader) As ArrayList
            Dim arr As New ArrayList
            Try
                Dim obj As RoleInfo
                While dr.Read
                    ' fill business object
                    obj = FillRoleInfo(dr, False)
                    ' add to collection
                    arr.Add(obj)
                End While
            Catch exc As Exception
                LogException(exc)
            Finally
                ' close datareader
                If Not dr Is Nothing Then
                    dr.Close()
                End If
            End Try
            Return arr
        End Function


#Region "Role Methods"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' CreateRole persists a Role to the Data Store
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <param name="portalId">Id of the portal</param>
        ''' <param name="role">The role to persist to the Data Store.</param>
        ''' <returns>A Boolean indicating success or failure.</returns>
        ''' <history>
        '''     [cnurse]	03/28/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Overrides Function CreateRole(ByVal portalId As Integer, ByRef role As RoleInfo) As Boolean
            Dim createStatus As Boolean = True

            Try
                role.RoleID = CType(dataProvider.AddRole(role.PortalID, role.RoleGroupID, role.RoleName, role.Description, role.ServiceFee, role.BillingPeriod.ToString, role.BillingFrequency, role.TrialFee, role.TrialPeriod, role.TrialFrequency, role.IsPublic, role.AutoAssignment, role.RSVPCode, role.IconFile), Integer)
            Catch ex As Exception
                'Clear User (duplicate User information)
                role = Nothing
                createStatus = False
            End Try

            Return createStatus

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' DeleteRole deletes a Role from the Data Store
        ''' </summary>
        ''' <param name="portalId">Id of the portal</param>
        ''' <param name="role">The role to delete from the Data Store.</param>
        ''' <history>
        '''     [cnurse]	03/28/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Overrides Sub DeleteRole(ByVal portalId As Integer, ByRef role As RoleInfo)
            dataProvider.DeleteRole(role.RoleID)
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetRole gets a role from the Data Store
        ''' </summary>
        ''' <remarks>This overload gets the role by its ID</remarks>
        ''' <param name="portalId">Id of the portal</param>
        ''' <param name="roleId">The Id of the role to retrieve.</param>
        ''' <returns>A RoleInfo object</returns>
        ''' <history>
        '''     [cnurse]	03/28/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Overloads Overrides Function GetRole(ByVal portalId As Integer, ByVal roleId As Integer) As RoleInfo
            Dim role As RoleInfo = Nothing
            Dim dr As IDataReader = dataProvider.GetRole(roleId, portalId)
            Try
                role = FillRoleInfo(dr)
            Finally
                If Not dr Is Nothing Then
                    dr.Close()
                End If
            End Try
            Return role
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetRole gets a role from the Data Store
        ''' </summary>
        ''' <remarks>This overload gets the role by its name</remarks>
        ''' <param name="portalId">Id of the portal</param>
        ''' <param name="roleName">The name of the role to retrieve.</param>
        ''' <returns>A RoleInfo object</returns>
        ''' <history>
        '''     [cnurse]	03/28/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Overloads Overrides Function GetRole(ByVal portalId As Integer, ByVal roleName As String) As RoleInfo
            Dim role As RoleInfo = Nothing
            Dim dr As IDataReader = dataProvider.GetRoleByName(portalId, roleName)
            Try
                role = FillRoleInfo(dr)
            Finally
                If Not dr Is Nothing Then
                    dr.Close()
                End If
            End Try
            Return role
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetRoleNames gets an array of roles for a portal
        ''' </summary>
        ''' <param name="portalId">Id of the portal</param>
        ''' <returns>A RoleInfo object</returns>
        ''' <history>
        '''     [cnurse]	03/28/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Overloads Overrides Function GetRoleNames(ByVal portalId As Integer) As String()

            Dim roles As String() = {}
            Dim strRoles As String = ""

            Dim arrRoles As ArrayList = GetRoles(portalId)
            For Each role As RoleInfo In arrRoles
                strRoles += role.RoleName + "|"
            Next

            If strRoles.IndexOf("|") > 0 Then
                roles = Split(Left(strRoles, strRoles.Length - 1), "|")
            End If

            Return roles

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetRoleNames gets an array of roles
        ''' </summary>
        ''' <param name="portalId">Id of the portal</param>
        ''' <param name="userId">The Id of the user whose roles are required. (If -1 then all
        ''' rolenames in a portal are retrieved.</param>
        ''' <returns>A RoleInfo object</returns>
        ''' <history>
        '''     [cnurse]	03/28/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Overloads Overrides Function GetRoleNames(ByVal portalId As Integer, ByVal userId As Integer) As String()

            Dim roles As String() = {}
            Dim strRoles As String = ""

            Dim dr As IDataReader = dataProvider.GetRolesByUser(userId, portalId)
            Try
                While dr.Read
                    strRoles += CType(dr("RoleName"), String) + "|"
                End While
            Finally
                If Not dr Is Nothing Then
                    dr.Close()
                End If
            End Try

            If strRoles.IndexOf("|") > 0 Then
                roles = Split(Left(strRoles, strRoles.Length - 1), "|")
            End If

            Return roles

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Get the roles for a portal
        ''' </summary>
        ''' <param name="portalId">Id of the portal (If -1 all roles for all portals are 
        ''' retrieved.</param>
        ''' <returns>An ArrayList of RoleInfo objects</returns>
        ''' <history>
        '''     [cnurse]	03/28/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Overrides Function GetRoles(ByVal portalId As Integer) As ArrayList
            Dim arrRoles As ArrayList

            If portalId = Null.NullInteger Then
                arrRoles = FillRoleInfoCollection(dataProvider.GetRoles())
            Else
                arrRoles = FillRoleInfoCollection(dataProvider.GetPortalRoles(portalId))
            End If

            Return arrRoles
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Get the roles for a Role Group
        ''' </summary>
        ''' <param name="portalId">Id of the portal</param>
        ''' <param name="roleGroupId">Id of the Role Group (If -1 all roles for the portal are
        ''' retrieved).</param>
        ''' <returns>An ArrayList of RoleInfo objects</returns>
        ''' <history>
        '''     [cnurse]	03/28/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Overrides Function GetRolesByGroup(ByVal portalId As Integer, ByVal roleGroupId As Integer) As ArrayList
            Return FillRoleInfoCollection(dataProvider.GetRolesByGroup(roleGroupId, portalId))
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Update a role
        ''' </summary>
        ''' <param name="role">The role to update</param>
        ''' <history>
        '''     [cnurse]	03/28/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Overrides Sub UpdateRole(ByVal role As RoleInfo)
            dataProvider.UpdateRole(role.RoleID, role.RoleGroupID, role.Description, role.ServiceFee, role.BillingPeriod.ToString, role.BillingFrequency, role.TrialFee, role.TrialPeriod, role.TrialFrequency, role.IsPublic, role.AutoAssignment, role.RSVPCode, role.IconFile)
        End Sub

#End Region

#Region "Role Group Methods"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' CreateRoleGroup persists a RoleGroup to the Data Store
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <param name="roleGroup">The RoleGroup to persist to the Data Store.</param>
        ''' <returns>The Id of the new role.</returns>
        ''' <history>
        '''     [cnurse]	03/28/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Overrides Function CreateRoleGroup(ByVal roleGroup As RoleGroupInfo) As Integer
            Return CType(dataProvider.AddRoleGroup(roleGroup.PortalID, roleGroup.RoleGroupName, roleGroup.Description), Integer)
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' DeleteRoleGroup deletes a RoleGroup from the Data Store
        ''' </summary>
        ''' <param name="roleGroup">The RoleGroup to delete from the Data Store.</param>
        ''' <history>
        '''     [cnurse]	03/28/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Overrides Sub DeleteRoleGroup(ByVal roleGroup As RoleGroupInfo)
            dataProvider.DeleteRoleGroup(roleGroup.RoleGroupID)
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetRoleGroup gets a RoleGroup from the Data Store
        ''' </summary>
        ''' <param name="portalId">Id of the portal</param>
        ''' <param name="roleGroupId">The Id of the RoleGroup to retrieve.</param>
        ''' <returns>A RoleGroupInfo object</returns>
        ''' <history>
        '''     [cnurse]	03/28/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Overrides Function GetRoleGroup(ByVal portalId As Integer, ByVal roleGroupId As Integer) As RoleGroupInfo
            Return CType(CBO.FillObject(dataProvider.GetRoleGroup(portalId, roleGroupId), GetType(RoleGroupInfo)), RoleGroupInfo)
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Get the RoleGroups for a portal
        ''' </summary>
        ''' <param name="portalId">Id of the portal.</param>
        ''' <returns>An ArrayList of RoleGroupInfo objects</returns>
        ''' <history>
        '''     [cnurse]	03/28/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Overrides Function GetRoleGroups(ByVal portalId As Integer) As ArrayList
            Return CType(CBO.FillCollection(dataProvider.GetRoleGroups(portalId), GetType(RoleGroupInfo)), ArrayList)
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Update a RoleGroup
        ''' </summary>
        ''' <param name="roleGroup">The RoleGroup to update</param>
        ''' <history>
        '''     [cnurse]	03/28/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Overrides Sub UpdateRoleGroup(ByVal roleGroup As RoleGroupInfo)
            dataProvider.UpdateRoleGroup(roleGroup.RoleGroupID, roleGroup.RoleGroupName, roleGroup.Description)
        End Sub

#End Region

#Region "User Role Methods"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' adds a DNN UserRole
        ''' </summary>
        ''' <param name="userRole">The role to add the user to.</param>
        ''' <history>
        '''     [cnurse]	03/28/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub AddDNNUserRole(ByVal userRole As UserRoleInfo)
            'Add UserRole to DNN
            userRole.UserRoleID = CType(dataProvider.AddUserRole(userRole.PortalID, userRole.UserID, userRole.RoleID, userRole.EffectiveDate, userRole.ExpiryDate), Integer)
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' AddUserToRole adds a User to a Role
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <param name="portalId">Id of the portal</param>
        ''' <param name="user">The user to add.</param>
        ''' <param name="userRole">The role to add the user to.</param>
        ''' <returns>A Boolean indicating success or failure.</returns>
        ''' <history>
        '''     [cnurse]	03/28/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Overrides Function AddUserToRole(ByVal portalId As Integer, ByVal user As UserInfo, ByVal userRole As UserRoleInfo) As Boolean
            Dim createStatus As Boolean = True

            Try
                'Add UserRole to DNN
                AddDNNUserRole(userRole)
            Catch ex As Exception
                'Clear User (duplicate User information)
                userRole = Nothing
                createStatus = False
            End Try

            Return createStatus
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetUserRole gets a User/Role object from the Data Store
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <param name="portalId">Id of the portal</param>
        ''' <param name="userId">The Id of the User</param>
        ''' <param name="roleId">The Id of the Role.</param>
        ''' <returns>The UserRoleInfo object</returns>
        ''' <history>
        '''     [cnurse]	03/28/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Overrides Function GetUserRole(ByVal portalId As Integer, ByVal userId As Integer, ByVal roleId As Integer) As UserRoleInfo
            Return CType(CBO.FillObject(dataProvider.GetUserRole(portalId, userId, roleId), GetType(UserRoleInfo)), UserRoleInfo)
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetUserRoles gets a collection of User/Role objects from the Data Store
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <param name="portalId">Id of the portal</param>
        ''' <param name="userId">The user to fetch roles for</param>
        ''' <returns>An ArrayList of UserRoleInfo objects</returns>
        ''' <history>
        '''     [cnurse]	03/28/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Overloads Overrides Function GetUserRoles(ByVal portalId As Integer, ByVal userId As Integer, ByVal includePrivate As Boolean) As ArrayList

            If includePrivate Then
                Return CBO.FillCollection(dataProvider.GetUserRoles(portalId, userId), GetType(UserRoleInfo))
            Else
                Return CBO.FillCollection(dataProvider.GetServices(portalId, userId), GetType(UserRoleInfo))
            End If
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetUserRoles gets a collection of User/Role objects from the Data Store
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <param name="portalId">Id of the portal</param>
        ''' <param name="userName">The user to fetch roles for</param>
        ''' <param name="roleName">The role to fetch users for</param>
        ''' <returns>An ArrayList of UserRoleInfo objects</returns>
        ''' <history>
        '''     [cnurse]	03/28/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Overloads Overrides Function GetUserRoles(ByVal portalId As Integer, ByVal userName As String, ByVal roleName As String) As ArrayList
            Return CBO.FillCollection(dataProvider.GetUserRolesByUsername(portalId, userName, roleName), GetType(UserRoleInfo))
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Get the users in a role (as UserRole objects)
        ''' </summary>
        ''' <param name="portalId">Id of the portal (If -1 all roles for all portals are 
        ''' retrieved.</param>
        ''' <param name="roleName">The role to fetch users for</param>
        ''' <returns>An ArrayList of UserRoleInfo objects</returns>
        ''' <history>
        '''     [cnurse]	03/28/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Overrides Function GetUserRolesByRoleName(ByVal portalId As Integer, ByVal roleName As String) As ArrayList
            Return GetUserRoles(portalId, Nothing, roleName)
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Get the users in a role (as User objects)
        ''' </summary>
        ''' <param name="portalId">Id of the portal (If -1 all roles for all portals are 
        ''' retrieved.</param>
        ''' <param name="roleName">The role to fetch users for</param>
        ''' <returns>An ArrayList of UserInfo objects</returns>
        ''' <history>
        '''     [cnurse]	03/28/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Overrides Function GetUsersByRoleName(ByVal portalId As Integer, ByVal roleName As String) As ArrayList
            Return CBO.FillCollection(dataProvider.GetUsersByRoleName(portalId, roleName), GetType(UserInfo))
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Remove a User from a Role
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <param name="portalId">Id of the portal</param>
        ''' <param name="user">The user to remove.</param>
        ''' <param name="userRole">The role to remove the user from.</param>
        ''' <history>
        '''     [cnurse]	03/28/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Overrides Sub RemoveUserFromRole(ByVal portalId As Integer, ByVal user As UserInfo, ByVal userRole As UserRoleInfo)
            dataProvider.DeleteUserRole(userRole.UserID, userRole.RoleID)
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Updates a User/Role
        ''' </summary>
        ''' <param name="userRole">The User/Role to update</param>
        ''' <history>
        '''     [cnurse]	12/15/2005	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Overrides Sub UpdateUserRole(ByVal userRole As UserRoleInfo)
            dataProvider.UpdateUserRole(userRole.UserRoleID, userRole.EffectiveDate, userRole.ExpiryDate)
        End Sub

#End Region

    End Class


End Namespace
