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
Imports System.Configuration
Imports System.Data
Imports System.Text.RegularExpressions
Imports System.Threading
Imports System.Web
Imports System.Web.Security

Imports DotNetNuke.Common
Imports DotNetNuke.Entities.Modules
Imports DotNetNuke.Entities.Portals
Imports DotNetNuke.Entities.Profile
Imports DotNetNuke.Security
Imports DotNetNuke.Security.Membership
Imports DotNetNuke.Security.Roles
Imports DotNetNuke.Services.Localization
Imports DotNetNuke.UI.WebControls

Namespace DotNetNuke.Entities.Users

    ''' -----------------------------------------------------------------------------
    ''' Project:    DotNetNuke
    ''' Namespace:  DotNetNuke.Entities.Users
    ''' Class:      UserController
    ''' -----------------------------------------------------------------------------
    ''' <summary>
    ''' The UserController class provides Business Layer methods for Users
    ''' </summary>
    ''' <remarks>
    ''' </remarks>
    ''' <history>
    '''     [cnurse]	02/18/2004	documented
    '''     [cnurse]    05/23/2005  made compatible with .NET 2.0
    ''' </history>
    ''' -----------------------------------------------------------------------------
    Public Class UserController

#Region "Private Shared Members"

        Private Shared memberProvider As DotNetNuke.Security.Membership.MembershipProvider = DotNetNuke.Security.Membership.MembershipProvider.Instance()

#End Region

#Region "Private Shared/Static Methods"

        Private Shared Sub AddEventLog(ByVal portalId As Integer, ByVal username As String, ByVal userId As Integer, ByVal portalName As String, ByVal Ip As String, ByVal loginStatus As UserLoginStatus)

            Dim objEventLog As New Services.Log.EventLog.EventLogController

            ' initialize log record
            Dim objEventLogInfo As New Services.Log.EventLog.LogInfo
            Dim objSecurity As New PortalSecurity
            objEventLogInfo.AddProperty("IP", Ip)
            objEventLogInfo.LogPortalID = portalId
            objEventLogInfo.LogPortalName = portalName
            objEventLogInfo.LogUserName = objSecurity.InputFilter(username, PortalSecurity.FilterFlag.NoScripting Or PortalSecurity.FilterFlag.NoAngleBrackets Or PortalSecurity.FilterFlag.NoMarkup)
            objEventLogInfo.LogUserID = userId

            ' create log record
            objEventLogInfo.LogTypeKey = loginStatus.ToString
            objEventLog.AddLog(objEventLogInfo)

        End Sub

#End Region

#Region "Shared/Static Methods"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' ChangePassword attempts to change the users password
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <param name="user">The user to update.</param>
        ''' <param name="oldPassword">The old password.</param>
        ''' <param name="newPassword">The new password.</param>
        ''' <returns>A Boolean indicating success or failure.</returns>
        ''' <history>
        '''     [cnurse]	12/13/2005	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function ChangePassword(ByVal user As UserInfo, ByVal oldPassword As String, ByVal newPassword As String) As Boolean

            Dim retValue As Boolean = Null.NullBoolean

            'Although we would hope that the caller has already validated the password,
            'Validate the new Password
            If ValidatePassword(newPassword) Then
                retValue = memberProvider.ChangePassword(user, oldPassword, newPassword)

                'Update User
                user.Membership.UpdatePassword = False
                UpdateUser(user.PortalID, user)
            Else
                Throw New Exception("Invalid Password")
            End If

            Return retValue

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' ChangePasswordQuestionAndAnswer attempts to change the users password Question
        ''' and PasswordAnswer
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <param name="user">The user to update.</param>
        ''' <param name="password">The password.</param>
        ''' <param name="passwordQuestion">The new password question.</param>
        ''' <param name="passwordAnswer">The new password answer.</param>
        ''' <returns>A Boolean indicating success or failure.</returns>
        ''' <history>
        '''     [cnurse]	02/08/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function ChangePasswordQuestionAndAnswer(ByVal user As UserInfo, ByVal password As String, ByVal passwordQuestion As String, ByVal passwordAnswer As String) As Boolean

            Return memberProvider.ChangePasswordQuestionAndAnswer(user, password, passwordQuestion, passwordAnswer)

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Creates a new User in the Data Store
        ''' </summary>
        ''' <remarks></remarks>
        ''' <param name="objUser">The userInfo object to persist to the Database</param>
        ''' <returns>The Created status ot the User</returns>
        ''' <history>
        ''' 	[cnurse]	12/13/2005	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function CreateUser(ByRef objUser As UserInfo) As UserCreateStatus

            Dim createStatus As UserCreateStatus = UserCreateStatus.AddUser

            'Create the User
            createStatus = memberProvider.CreateUser(objUser)

            If createStatus = UserCreateStatus.Success Then
                DataCache.ClearPortalCache(objUser.PortalID, False)

                If Not objUser.IsSuperUser Then

                    Dim objRoles As New RoleController
                    Dim objRole As RoleInfo

                    ' autoassign user to portal roles
                    Dim arrRoles As ArrayList = objRoles.GetPortalRoles(objUser.PortalID)
                    Dim i As Integer
                    For i = 0 To arrRoles.Count - 1
                        objRole = CType(arrRoles(i), RoleInfo)
                        If objRole.AutoAssignment = True Then
                            objRoles.AddUserRole(objUser.PortalID, objUser.UserID, objRole.RoleID, Null.NullDate, Null.NullDate)
                        End If
                    Next
                End If
            End If

            Return createStatus

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Deletes an existing User from the Data Store
        ''' </summary>
        ''' <remarks></remarks>
        ''' <param name="objUser">The userInfo object to delete from the Database</param>
        ''' <param name="notify">A flag that indicates whether an email notification should be sent</param>
        ''' <param name="deleteAdmin">A flag that indicates whether the Portal Administrator should be deleted</param>
        ''' <returns>A Boolean value that indicates whether the User was successfully deleted</returns>
        ''' <history>
        ''' 	[cnurse]	12/13/2005	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function DeleteUser(ByRef objUser As UserInfo, ByVal notify As Boolean, ByVal deleteAdmin As Boolean) As Boolean

            Dim CanDelete As Boolean = True

            Try
                'Determine if the User is the Portal Administrator
                Dim dr As IDataReader
                dr = DataProvider.Instance().GetPortal(objUser.PortalID)
                If dr.Read Then
                    If objUser.UserID = Convert.ToInt32(dr("AdministratorId")) Then
                        CanDelete = deleteAdmin
                    End If
                End If
                If Not dr Is Nothing Then
                    dr.Close()
                End If

                If CanDelete Then
                    'Delete Folder Permissions
                    Dim objFolderPermissionController As New Permissions.FolderPermissionController()
                    objFolderPermissionController.DeleteFolderPermissionsByUserID(objUser)

                    'Delete Module Permissions
                    Dim objModulePermissionController As New Permissions.ModulePermissionController()
                    objModulePermissionController.DeleteModulePermissionsByUserID(objUser)

                    'Delete Tab Permissions
                    Dim objTabPermissionController As New Permissions.TabPermissionController()
                    objTabPermissionController.DeleteTabPermissionsByUserID(objUser)


                    CanDelete = memberProvider.DeleteUser(objUser)
                End If

                If CanDelete Then
                    ' Obtain PortalSettings from Current Context
                    Dim _portalSettings As PortalSettings = PortalController.GetCurrentPortalSettings

                    'Log event
                    Dim objEventLog As New Services.Log.EventLog.EventLogController
                    objEventLog.AddLog("Username", objUser.Username, _portalSettings, objUser.UserID, Services.Log.EventLog.EventLogController.EventLogType.USER_DELETED)

                    If notify Then
                        ' send email notification to portal administrator that the user was removed from the portal
                        DotNetNuke.Services.Mail.Mail.SendMail(_portalSettings.Email, _portalSettings.Email, "", _
                            Localization.GetSystemMessage(objUser.Profile.PreferredLocale, _portalSettings, "EMAIL_USER_UNREGISTER_SUBJECT", objUser, Localization.GlobalResourceFile, Nothing, "", _portalSettings.AdministratorId), _
                            Localization.GetSystemMessage(objUser.Profile.PreferredLocale, _portalSettings, "EMAIL_USER_UNREGISTER_BODY", objUser, Localization.GlobalResourceFile, Nothing, "", _portalSettings.AdministratorId), _
                            "", "", "", "", "", "")
                    End If
                    DataCache.ClearPortalCache(objUser.PortalID, False)
                    DataCache.ClearUserCache(objUser.PortalID, objUser.Username)
                End If

            Catch Exc As Exception
                CanDelete = False
            End Try

            Return CanDelete

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Deletes all Users for a Portal
        ''' </summary>
        ''' <remarks></remarks>
        ''' <param name="portalId">The Id of the Portal</param>
        ''' <param name="notify">A flag that indicates whether an email notification should be sent</param>
        ''' <param name="deleteAdmin">A flag that indicates whether the Portal Administrator should be deleted</param>
        ''' <history>
        ''' 	[cnurse]	12/14/2005	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub DeleteUsers(ByVal portalId As Integer, ByVal notify As Boolean, ByVal deleteAdmin As Boolean)

            Dim arrUsers As ArrayList = GetUsers(portalId, False)

            For Each objUser As UserInfo In arrUsers
                DeleteUser(objUser, notify, deleteAdmin)
            Next

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Deletes all Unauthorized Users for a Portal
        ''' </summary>
        ''' <remarks></remarks>
        ''' <param name="portalId">The Id of the Portal</param>
        ''' <history>
        ''' 	[cnurse]	12/14/2005	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub DeleteUnauthorizedUsers(ByVal portalId As Integer)

            Dim arrUsers As ArrayList = GetUsers(portalId, False)

            For Each objUser As UserInfo In arrUsers
                If objUser.Membership.Approved = False Or objUser.Membership.LastLoginDate = Null.NullDate Then
                    DeleteUser(objUser, True, False)
                End If
            Next

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Generates a new random password (Length = Minimum Length + 4)
        ''' </summary>
        ''' <returns>A String</returns>
        ''' <history>
        '''     [cnurse]	03/08/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GeneratePassword() As String

            Return GeneratePassword(MembershipProviderConfig.MinPasswordLength + 4)

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Generates a new random password
        ''' </summary>
        ''' <param name="length">The length of password to generate.</param>
        ''' <returns>A String</returns>
        ''' <history>
        '''     [cnurse]	03/08/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GeneratePassword(ByVal length As Integer) As String

            Return memberProvider.GeneratePassword(length)

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetCachedUser retrieves the User from the Cache, or fetches a fresh copy if 
        ''' not in cache or if Cache settings not set to HeavyCaching
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <param name="portalId">The Id of the Portal</param>
        ''' <param name="username">The username of the user being retrieved.</param>
        ''' <returns>The User as a UserInfo object</returns>
        ''' <history>
        '''     [cnurse]	12/12/2005	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetCachedUser(ByVal portalId As Integer, ByVal username As String) As UserInfo

            'Get the User cache key
            Dim key As String = String.Format(DataCache.UserCacheKey, portalId, username)

            Dim objUserInfo As UserInfo = CType(DataCache.GetCache(key), UserInfo)

            'if user is not cached or user does not correspod to the provided username - fetch from db
            If objUserInfo Is Nothing OrElse objUserInfo.Username <> username Then
                'user caching settings
                Dim timeOut As Int32 = DataCache.UserCacheTimeOut * Convert.ToInt32(Common.Globals.PerformanceSetting)

                objUserInfo = UserController.GetUserByName(portalId, username, False)
                If objUserInfo IsNot Nothing AndAlso timeOut > 0 Then
                    DataCache.SetCache(key, objUserInfo, TimeSpan.FromMinutes(timeOut))
                End If
            End If

            Return objUserInfo

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Get the current UserInfo object
        ''' </summary>
        ''' <returns>The current UserInfo if authenticated, oherwise an empty user</returns>
        ''' <history>
        ''' 	[cnurse]	05/23/2005	Documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetCurrentUserInfo() As UserInfo

            If (HttpContext.Current Is Nothing) Then
                If Not (Thread.CurrentPrincipal.Identity.IsAuthenticated) Then
                    Return New UserInfo
                Else
                    Dim _portalSettings As PortalSettings = PortalController.GetCurrentPortalSettings
                    Dim objUser As UserInfo = GetUserByName(_portalSettings.PortalId, Thread.CurrentPrincipal.Identity.Name, False)
                    If Not objUser Is Nothing Then
                        Return objUser
                    Else
                        Return New UserInfo
                    End If
                End If
            Else
                Dim objUser As UserInfo = CType(HttpContext.Current.Items("UserInfo"), UserInfo)
                If Not objUser Is Nothing Then
                    Return objUser
                Else
                    Return New UserInfo
                End If
            End If
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets a collection of Online Users
        ''' </summary>
        ''' <param name="portalId">The Id of the Portal</param>
        ''' <returns>An ArrayList of UserInfo objects</returns>
        ''' <history>
        '''     [cnurse]	03/15/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetOnlineUsers(ByVal PortalId As Integer) As ArrayList

            Return memberProvider.GetOnlineUsers(PortalId)

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the Current Password Information for the User 
        ''' </summary>
        ''' <remarks>This method will only return the password if the memberProvider supports
        ''' and is using a password encryption method that supports decryption.</remarks>
        ''' <param name="user">The user whose Password information we are retrieving.</param>
        ''' <param name="passwordAnswer">The answer to the "user's" password Question.</param>
        ''' <history>
        ''' 	[cnurse]	12/13/2005	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetPassword(ByRef user As UserInfo, ByVal passwordAnswer As String) As String

            If MembershipProviderConfig.PasswordRetrievalEnabled Then
                user.Membership.Password = memberProvider.GetPassword(user, passwordAnswer)
            Else
                'Throw a configuration exception as password retrieval is not enabled
                Throw New ConfigurationErrorsException("Password Retrieval is not enabled")
            End If

            Return user.Membership.Password

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetUnAuthorizedUsers gets all the users of the portal, that are not authorized
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <param name="portalId">The Id of the Portal</param>
        ''' <returns>An ArrayList of UserInfo objects.</returns>
        ''' <history>
        '''     [cnurse]	7/11/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetUnAuthorizedUsers(ByVal portalId As Integer) As ArrayList

            Return memberProvider.GetUnAuthorizedUsers(portalId, False)

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetUnAuthorizedUsers gets all the users of the portal, that are not authorized
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <param name="portalId">The Id of the Portal</param>
        ''' <param name="isHydrated">A flag that determines whether the user is hydrated.</param>
        ''' <returns>An ArrayList of UserInfo objects.</returns>
        ''' <history>
        '''     [cnurse]	12/14/2005	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetUnAuthorizedUsers(ByVal portalId As Integer, ByVal isHydrated As Boolean) As ArrayList

            Return memberProvider.GetUnAuthorizedUsers(portalId, isHydrated)

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetUser retrieves a User from the DataStore
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <param name="portalId">The Id of the Portal</param>
        ''' <param name="userId">The Id of the user being retrieved from the Data Store.</param>
        ''' <param name="isHydrated">A flag that determines whether the user is hydrated.</param>
        ''' <returns>The User as a UserInfo object</returns>
        ''' <history>
        '''     [cnurse]	12/10/2005	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetUser(ByVal portalId As Integer, ByVal userId As Integer, ByVal isHydrated As Boolean) As UserInfo

            Return memberProvider.GetUser(portalId, userId, isHydrated)

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetUser retrieves a User from the DataStore
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <param name="portalId">The Id of the Portal</param>
        ''' <param name="userId">The Id of the user being retrieved from the Data Store.</param>
        ''' <param name="isHydrated">A flag that determines whether the user is hydrated.</param>
        ''' <param name="hydrateRoles">A flag that instructs the method to automatically hydrate the roles</param>
        ''' <returns>The User as a UserInfo object</returns>
        ''' <history>
        ''' </history>
        ''' -----------------------------------------------------------------------------
        <Obsolete("Not needed any longer for single users due to autohydration")> _
        Public Shared Function GetUser(ByVal portalId As Integer, ByVal userId As Integer, ByVal isHydrated As Boolean, ByVal hydrateRoles As Boolean) As UserInfo

            Dim user As UserInfo = GetUser(portalId, userId, isHydrated)

            If hydrateRoles Then
                Dim controller As DotNetNuke.Security.Roles.RoleController = New DotNetNuke.Security.Roles.RoleController
                user.Roles = controller.GetRolesByUser(userId, portalId)
            End If

            Return user

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetUserByUserName retrieves a User from the DataStore
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <param name="portalId">The Id of the Portal</param>
        ''' <param name="username">The username of the user being retrieved from the Data Store.</param>
        ''' <returns>The User as a UserInfo object</returns>
        ''' <history>
        '''     [cnurse]	7/11/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetUserByName(ByVal portalId As Integer, ByVal username As String) As UserInfo

            Return memberProvider.GetUserByUserName(portalId, username, False)

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetUserByUserName retrieves a User from the DataStore
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <param name="portalId">The Id of the Portal</param>
        ''' <param name="username">The username of the user being retrieved from the Data Store.</param>
        ''' <param name="isHydrated">A flag that determines whether the user is hydrated.</param>
        ''' <returns>The User as a UserInfo object</returns>
        ''' <history>
        '''     [cnurse]	12/10/2005	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetUserByName(ByVal portalId As Integer, ByVal username As String, ByVal isHydrated As Boolean) As UserInfo

            Return memberProvider.GetUserByUserName(portalId, username, isHydrated)

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetUserCountByPortal gets the number of users in the portal
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <param name="portalId">The Id of the Portal</param>
        ''' <returns>The no of users</returns>
        ''' <history>
        '''     [cnurse]	05/01/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetUserCountByPortal(ByVal portalId As Integer) As Integer

            Return memberProvider.GetUserCountByPortal(portalId)

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Retruns a String corresponding to the Registration Status of the User
        ''' </summary>
        ''' <param name="UserRegistrationStatus">The AUserCreateStatus</param>
        ''' <returns>A String</returns>
        ''' <history>
        ''' 	[cnurse]	12/13/2005	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetUserCreateStatus(ByVal UserRegistrationStatus As UserCreateStatus) As String
            Select Case UserRegistrationStatus
                Case UserCreateStatus.DuplicateEmail
                    Return Localization.GetString("UserEmailExists")
                Case UserCreateStatus.InvalidAnswer
                    Return Localization.GetString("InvalidAnswer")
                Case UserCreateStatus.InvalidEmail
                    Return Localization.GetString("InvalidEmail")
                Case UserCreateStatus.InvalidPassword
                    Dim strInvalidPassword As String = Localization.GetString("InvalidPassword")
                    strInvalidPassword = strInvalidPassword.Replace("[PasswordLength]", MembershipProviderConfig.MinPasswordLength.ToString)
                    strInvalidPassword = strInvalidPassword.Replace("[NoneAlphabet]", MembershipProviderConfig.MinNonAlphanumericCharacters.ToString)
                    Return strInvalidPassword
                Case UserCreateStatus.PasswordMismatch
                    Return Localization.GetString("PasswordMismatch")
                Case UserCreateStatus.InvalidQuestion
                    Return Localization.GetString("InvalidQuestion")
                Case UserCreateStatus.InvalidUserName
                    Return Localization.GetString("InvalidUserName")
                Case UserCreateStatus.UserRejected
                    Return Localization.GetString("UserRejected")
                Case UserCreateStatus.DuplicateUserName, UserCreateStatus.UserAlreadyRegistered, UserCreateStatus.UsernameAlreadyExists
                    Return Localization.GetString("UserNameExists")
                Case UserCreateStatus.ProviderError, UserCreateStatus.DuplicateProviderUserKey, UserCreateStatus.InvalidProviderUserKey, UserCreateStatus.UnexpectedError
                    Return Localization.GetString("RegError")
                Case Else
                    Throw New ArgumentException("Unknown UserCreateStatus value encountered", "UserRegistrationStatus")
            End Select
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the Membership Information for the User
        ''' </summary>
        ''' <remarks></remarks>
        ''' <param name="objUser">The user whose Membership information we are retrieving.</param>
        ''' <history>
        ''' 	[cnurse]	12/13/2005	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub GetUserMembership(ByRef objUser As UserInfo)

            memberProvider.GetUserMembership(objUser)

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetUserSettings retrieves the UserSettings from the User
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <param name="portalId">The Id of the Portal</param>
        ''' <returns>The Settings Hashtable</returns>
        ''' <history>
        '''     [cnurse]	03/23/2005	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetUserSettings(ByVal portalId As Integer) As Hashtable

            Dim SettingsCacheKey As String = SettingsKey(portalId)
            Dim settings As Hashtable = CType(DataCache.GetCache(SettingsCacheKey), Hashtable)
            If settings Is Nothing Then
                Dim objModules As New ModuleController
                Dim objModule As ModuleInfo = objModules.GetModuleByDefinition(portalId, "User Accounts")
                If Not objModule Is Nothing Then
                    settings = objModules.GetModuleSettings(objModule.ModuleID)
                    DataCache.SetCache(SettingsCacheKey, settings, TimeSpan.FromMinutes(Globals.PerformanceSetting))
                End If
            End If

            Return settings

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetUsers gets all the users of the portal
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <param name="portalId">The Id of the Portal</param>
        ''' <returns>An ArrayList of UserInfo objects.</returns>
        ''' <history>
        '''     [cnurse]	7/11/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetUsers(ByVal portalId As Integer) As ArrayList

            Return GetUsers(portalId, False, -1, -1, -1)

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetUsers gets all the users of the portal
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <param name="portalId">The Id of the Portal</param>
        ''' <param name="isHydrated">A flag that determines whether the user is hydrated.</param>
        ''' <returns>An ArrayList of UserInfo objects.</returns>
        ''' <history>
        '''     [cnurse]	12/14/2005	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetUsers(ByVal portalId As Integer, ByVal isHydrated As Boolean) As ArrayList

            Return GetUsers(portalId, isHydrated, -1, -1, -1)

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetUsers gets all the users of the portal, by page
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <param name="portalId">The Id of the Portal</param>
        ''' <param name="pageIndex">The page of records to return.</param>
        ''' <param name="pageSize">The size of the page</param>
        ''' <param name="totalRecords">The total no of records that satisfy the criteria.</param>
        ''' <returns>An ArrayList of UserInfo objects.</returns>
        ''' <history>
        '''     [cnurse]	7/11/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetUsers(ByVal portalId As Integer, ByVal pageIndex As Integer, ByVal pageSize As Integer, ByRef totalRecords As Integer) As ArrayList

            Return memberProvider.GetUsers(portalId, False, pageIndex, pageSize, totalRecords)

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetUsers gets all the users of the portal, by page
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <param name="portalId">The Id of the Portal</param>
        ''' <param name="pageIndex">The page of records to return.</param>
        ''' <param name="pageSize">The size of the page</param>
        ''' <param name="totalRecords">The total no of records that satisfy the criteria.</param>
        ''' <returns>An ArrayList of UserInfo objects.</returns>
        ''' <history>
        '''     [cnurse]	12/14/2005	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetUsers(ByVal portalId As Integer, ByVal isHydrated As Boolean, ByVal pageIndex As Integer, ByVal pageSize As Integer, ByRef totalRecords As Integer) As ArrayList

            Return memberProvider.GetUsers(portalId, isHydrated, pageIndex, pageSize, totalRecords)

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetUsersByEmail gets all the users of the portal whose email matches a provided
        ''' filter expression
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <param name="portalId">The Id of the Portal</param>
        ''' <param name="emailToMatch">The email address to use to find a match.</param>
        ''' <param name="pageIndex">The page of records to return.</param>
        ''' <param name="pageSize">The size of the page</param>
        ''' <param name="totalRecords">The total no of records that satisfy the criteria.</param>
        ''' <returns>An ArrayList of UserInfo objects.</returns>
        ''' <history>
        '''     [cnurse]	7/11/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetUsersByEmail(ByVal portalId As Integer, ByVal emailToMatch As String, ByVal pageIndex As Integer, ByVal pageSize As Integer, ByRef totalRecords As Integer) As ArrayList

            Return memberProvider.GetUsersByEmail(portalId, False, emailToMatch, pageIndex, pageSize, totalRecords)

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetUsersByEmail gets all the users of the portal whose email matches a provided
        ''' filter expression
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <param name="portalId">The Id of the Portal</param>
        ''' <param name="isHydrated">A flag that determines whether the user is hydrated.</param>
        ''' <param name="emailToMatch">The email address to use to find a match.</param>
        ''' <param name="pageIndex">The page of records to return.</param>
        ''' <param name="pageSize">The size of the page</param>
        ''' <param name="totalRecords">The total no of records that satisfy the criteria.</param>
        ''' <returns>An ArrayList of UserInfo objects.</returns>
        ''' <history>
        '''     [cnurse]	12/14/2005	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetUsersByEmail(ByVal portalId As Integer, ByVal isHydrated As Boolean, ByVal emailToMatch As String, ByVal pageIndex As Integer, ByVal pageSize As Integer, ByRef totalRecords As Integer) As ArrayList

            Return memberProvider.GetUsersByEmail(portalId, isHydrated, emailToMatch, pageIndex, pageSize, totalRecords)

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetUsersByUserName gets all the users of the portal whose username matches a provided
        ''' filter expression
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <param name="portalId">The Id of the Portal</param>
        ''' <param name="userNameToMatch">The username to use to find a match.</param>
        ''' <param name="pageIndex">The page of records to return.</param>
        ''' <param name="pageSize">The size of the page</param>
        ''' <param name="totalRecords">The total no of records that satisfy the criteria.</param>
        ''' <returns>An ArrayList of UserInfo objects.</returns>
        ''' <history>
        '''     [cnurse]	7/11/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetUsersByUserName(ByVal portalId As Integer, ByVal userNameToMatch As String, ByVal pageIndex As Integer, ByVal pageSize As Integer, ByRef totalRecords As Integer) As ArrayList

            Return memberProvider.GetUsersByUserName(portalId, False, userNameToMatch, pageIndex, pageSize, totalRecords)

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetUsersByUserName gets all the users of the portal whose username matches a provided
        ''' filter expression
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <param name="portalId">The Id of the Portal</param>
        ''' <param name="isHydrated">A flag that determines whether the user is hydrated.</param>
        ''' <param name="userNameToMatch">The username to use to find a match.</param>
        ''' <param name="pageIndex">The page of records to return.</param>
        ''' <param name="pageSize">The size of the page</param>
        ''' <param name="totalRecords">The total no of records that satisfy the criteria.</param>
        ''' <returns>An ArrayList of UserInfo objects.</returns>
        ''' <history>
        '''     [cnurse]	12/14/2005	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetUsersByUserName(ByVal portalId As Integer, ByVal isHydrated As Boolean, ByVal userNameToMatch As String, ByVal pageIndex As Integer, ByVal pageSize As Integer, ByRef totalRecords As Integer) As ArrayList

            Return memberProvider.GetUsersByUserName(portalId, isHydrated, userNameToMatch, pageIndex, pageSize, totalRecords)

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetUsersByProfileProperty gets all the users of the portal whose profile matches
        ''' the profile property pased as a parameter
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <param name="portalId">The Id of the Portal</param>
        ''' <param name="propertyName">The name of the property being matched.</param>
        ''' <param name="propertyValue">The value of the property being matched.</param>
        ''' <param name="pageIndex">The page of records to return.</param>
        ''' <param name="pageSize">The size of the page</param>
        ''' <param name="totalRecords">The total no of records that satisfy the criteria.</param>
        ''' <returns>An ArrayList of UserInfo objects.</returns>
        ''' <history>
        '''     [cnurse]	7/11/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetUsersByProfileProperty(ByVal portalId As Integer, ByVal propertyName As String, ByVal propertyValue As String, ByVal pageIndex As Integer, ByVal pageSize As Integer, ByRef totalRecords As Integer) As ArrayList

            Return memberProvider.GetUsersByProfileProperty(portalId, False, propertyName, propertyValue, pageIndex, pageSize, totalRecords)

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetUsersByProfileProperty gets all the users of the portal whose profile matches
        ''' the profile property pased as a parameter
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <param name="portalId">The Id of the Portal</param>
        ''' <param name="isHydrated">A flag that determines whether the user is hydrated.</param>
        ''' <param name="propertyName">The name of the property being matched.</param>
        ''' <param name="propertyValue">The value of the property being matched.</param>
        ''' <param name="pageIndex">The page of records to return.</param>
        ''' <param name="pageSize">The size of the page</param>
        ''' <param name="totalRecords">The total no of records that satisfy the criteria.</param>
        ''' <returns>An ArrayList of UserInfo objects.</returns>
        ''' <history>
        '''     [cnurse]	02/01/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetUsersByProfileProperty(ByVal portalId As Integer, ByVal isHydrated As Boolean, ByVal propertyName As String, ByVal propertyValue As String, ByVal pageIndex As Integer, ByVal pageSize As Integer, ByRef totalRecords As Integer) As ArrayList

            Return memberProvider.GetUsersByProfileProperty(portalId, isHydrated, propertyName, propertyValue, pageIndex, pageSize, totalRecords)

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Resets the password for the specified user
        ''' </summary>
        ''' <remarks>Resets the user's password</remarks>
        ''' <param name="user">The user whose Password information we are resetting.</param>
        ''' <param name="passwordAnswer">The answer to the "user's" password Question.</param>
        ''' <history>
        ''' 	[cnurse]	02/08/2006	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function ResetPassword(ByVal user As UserInfo, ByVal passwordAnswer As String) As String

            If MembershipProviderConfig.PasswordResetEnabled Then
                user.Membership.Password = memberProvider.ResetPassword(user, passwordAnswer)
            Else
                'Throw a configuration exception as password reset is not enabled
                Throw New ConfigurationErrorsException("Password Reset is not enabled")
            End If

            Return user.Membership.Password

        End Function

        Public Shared Sub SetAuthCookie(ByVal username As String, ByVal CreatePersistentCookie As Boolean)


        End Sub

        Public Shared Function SettingsKey(ByVal portalId As Integer) As String
            Return "UserSettings|" + portalId.ToString
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Unlocks the User's Account
        ''' </summary>
        ''' <remarks></remarks>
        ''' <param name="user">The user whose account is being Unlocked.</param>
        ''' <history>
        ''' 	[cnurse]	12/13/2005	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function UnLockUser(ByVal user As UserInfo) As Boolean

            Dim retValue As Boolean = False

            'Unlock the User
            retValue = memberProvider.UnLockUser(user)

            DataCache.ClearUserCache(user.PortalID, user.Username)

            Return retValue

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Updates a User
        ''' </summary>
        ''' <param name="portalId">The Id of the Portal</param>
        ''' <param name="objUser">The use to update</param>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	02/18/2005	Documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub UpdateUser(ByVal portalId As Integer, ByVal objUser As UserInfo)
            'Update the User
            memberProvider.UpdateUser(objUser)

            'Remove the UserInfo from the Cache, as it has been modified
            DataCache.ClearUserCache(portalId, objUser.Username)
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Validates a User's credentials against the Data Store, and sets the Forms Authentication
        ''' Ticket
        ''' </summary>
        ''' <param name="portalId">The Id of the Portal the user belongs to</param>
        ''' <param name="UserName">The user name of the User attempting to log in</param>
        ''' <param name="Password">The password of the User attempting to log in</param>
        ''' <param name="VerificationCode">The verification code of the User attempting to log in</param>
        ''' <param name="PortalName">The name of the Portal</param>
        ''' <param name="IP">The IP Address of the user attempting to log in</param>
        ''' <param name="loginStatus">A UserLoginStatus enumeration that indicates the status of the 
        ''' Login attempt.  This value is returned by reference.</param>
        ''' <param name="CreatePersistentCookie">A flag that indicates whether the login credentials 
        ''' should be persisted.</param>
        ''' <returns>The UserInfo object representing a successful login</returns>
        ''' <history>
        ''' 	[cnurse]	12/09/2005	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function UserLogin(ByVal portalId As Integer, ByVal Username As String, ByVal Password As String, ByVal VerificationCode As String, ByVal PortalName As String, ByVal IP As String, ByRef loginStatus As UserLoginStatus, ByVal CreatePersistentCookie As Boolean) As UserInfo

            loginStatus = UserLoginStatus.LOGIN_FAILURE

            'Validate the user
            Dim objUser As UserInfo = ValidateUser(portalId, Username, Password, VerificationCode, PortalName, IP, loginStatus)

            If Not objUser Is Nothing Then
                'Call UserLogin overload
                UserLogin(portalId, objUser, PortalName, IP, CreatePersistentCookie)
            Else
                AddEventLog(portalId, Username, Null.NullInteger, PortalName, IP, loginStatus)
            End If

            ' return the User object
            Return objUser

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Logs a Validated User in
        ''' </summary>
        ''' <param name="portalId">The Id of the Portal the user belongs to</param>
        ''' <param name="user">The validated User</param>
        ''' <param name="PortalName">The name of the Portal</param>
        ''' <param name="IP">The IP Address of the user attempting to log in</param>
        ''' <param name="CreatePersistentCookie">A flag that indicates whether the login credentials 
        ''' should be persisted.</param>
        ''' <history>
        ''' 	[cnurse]	03/15/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub UserLogin(ByVal portalId As Integer, ByVal user As UserInfo, ByVal PortalName As String, ByVal IP As String, ByVal CreatePersistentCookie As Boolean)

            If user.IsSuperUser Then
                AddEventLog(portalId, user.Username, user.UserID, PortalName, IP, UserLoginStatus.LOGIN_SUPERUSER)
            Else
                AddEventLog(portalId, user.Username, user.UserID, PortalName, IP, UserLoginStatus.LOGIN_SUCCESS)
            End If

            ' set the forms authentication cookie ( log the user in )
            FormsAuthentication.SetAuthCookie(user.Username, CreatePersistentCookie)

            'check if cookie is persistent, and user has supplied custom value for expiration
            If CreatePersistentCookie = True Then
                Dim PersistentCookieTimeout As Integer
                If Not Config.GetSetting("PersistentCookieTimeout") Is Nothing Then
                    PersistentCookieTimeout = Integer.Parse(Config.GetSetting("PersistentCookieTimeout"))
                    'only use if non-zero, otherwise leave as asp.net value    
                    If PersistentCookieTimeout <> 0 Then
                        'manually create authentication cookie    
                        'first, create the authentication ticket     
                        Dim AuthenticationTicket As FormsAuthenticationTicket = New FormsAuthenticationTicket(user.Username, True, PersistentCookieTimeout)
                        'encrypt it     
                        Dim EncryptedAuthTicket As String = FormsAuthentication.Encrypt(AuthenticationTicket)
                        Dim AuthCookie As HttpCookie = New HttpCookie(FormsAuthentication.FormsCookieName, EncryptedAuthTicket)
                        'set cookie expiration to correspond with ticket expiration.  
                        AuthCookie.Expires = AuthenticationTicket.Expiration
                        HttpContext.Current.Response.Cookies.Set(AuthCookie)
                    End If
                End If
            End If

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Validates a Password
        ''' </summary>
        ''' <param name="password">The password to Validate</param>
        ''' <returns>A boolean</returns>
        ''' <history>
        ''' 	[cnurse]	02/08/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function ValidatePassword(ByVal password As String) As Boolean

            Dim isValid As Boolean = True
            Dim rx As Regex

            'Valid Length
            If password.Length < MembershipProviderConfig.MinPasswordLength Then
                isValid = False
            End If

            'Validate NonAlphaChars
            rx = New Regex("[^0-9a-zA-Z]")
            If rx.Matches(password).Count < MembershipProviderConfig.MinNonAlphanumericCharacters Then
                isValid = False
            End If

            'Validate Regex
            If MembershipProviderConfig.PasswordStrengthRegularExpression <> "" Then
                rx = New Regex(MembershipProviderConfig.PasswordStrengthRegularExpression)
                isValid = rx.IsMatch(password)
            End If

            Return isValid

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Validates a User's credentials against the Data Store
        ''' </summary>
        ''' <param name="portalId">The Id of the Portal the user belongs to</param>
        ''' <param name="UserName">The user name of the User attempting to log in</param>
        ''' <param name="Password">The password of the User attempting to log in</param>
        ''' <param name="VerificationCode">The verification code of the User attempting to log in</param>
        ''' <param name="PortalName">The name of the Portal</param>
        ''' <param name="IP">The IP Address of the user attempting to log in</param>
        ''' <param name="loginStatus">A UserLoginStatus enumeration that indicates the status of the 
        ''' Login attempt.  This value is returned by reference.</param>
        ''' <returns>The UserInfo object representing a valid user</returns>
        ''' <history>
        ''' 	[cnurse]	03/15/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function ValidateUser(ByVal portalId As Integer, ByVal Username As String, ByVal Password As String, ByVal VerificationCode As String, ByVal PortalName As String, ByVal IP As String, ByRef loginStatus As UserLoginStatus) As UserInfo
            Return ValidateUser(portalId, Username, Password, "DNN", VerificationCode, PortalName, IP, loginStatus)
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Validates a User's credentials against the Data Store
        ''' </summary>
        ''' <param name="portalId">The Id of the Portal the user belongs to</param>
        ''' <param name="UserName">The user name of the User attempting to log in</param>
        ''' <param name="Password">The password of the User attempting to log in</param>
        ''' <param name="authType">The type of Authentication Used</param>
        ''' <param name="VerificationCode">The verification code of the User attempting to log in</param>
        ''' <param name="PortalName">The name of the Portal</param>
        ''' <param name="IP">The IP Address of the user attempting to log in</param>
        ''' <param name="loginStatus">A UserLoginStatus enumeration that indicates the status of the 
        ''' Login attempt.  This value is returned by reference.</param>
        ''' <returns>The UserInfo object representing a valid user</returns>
        ''' <history>
        '''     [cnurse]	07/09/2007	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function ValidateUser(ByVal portalId As Integer, ByVal Username As String, ByVal Password As String, ByVal authType As String, ByVal VerificationCode As String, ByVal PortalName As String, ByVal IP As String, ByRef loginStatus As UserLoginStatus) As UserInfo
            loginStatus = UserLoginStatus.LOGIN_FAILURE

            'Try and Log the user in
            Dim objUser As UserInfo = memberProvider.UserLogin(portalId, Username, Password, authType, VerificationCode, loginStatus)

            If loginStatus = UserLoginStatus.LOGIN_USERLOCKEDOUT Or loginStatus = UserLoginStatus.LOGIN_FAILURE Then
                'User Locked Out so log to event log
                AddEventLog(portalId, Username, Null.NullInteger, PortalName, IP, loginStatus)
            End If

            'Check Default Accounts
            If loginStatus = UserLoginStatus.LOGIN_SUCCESS Then
                If Username = "admin" AndAlso (Password = "admin" Or Password = "dnnadmin") Then
                    loginStatus = UserLoginStatus.LOGIN_INSECUREADMINPASSWORD
                End If
            End If
            If loginStatus = UserLoginStatus.LOGIN_SUPERUSER Then
                If Username = "host" AndAlso (Password = "host" Or Password = "dnnhost") Then
                    loginStatus = UserLoginStatus.LOGIN_INSECUREHOSTPASSWORD
                End If
            End If

            ' return the User object
            Return objUser
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Validates a User's Password and Profile
        ''' </summary>
        ''' <remarks>This overload takes a valid User (Credentials check out) and check whether the Password and Profile need updating</remarks>
        ''' <param name="portalId">The Id of the Portal the user belongs to</param>
        ''' <param name="objUser">The user attempting to log in</param>
        ''' <returns>The UserLoginStatus</returns>
        ''' <history>
        ''' 	[cnurse]	07/03/2007	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function ValidateUser(ByVal objUser As UserInfo, ByVal portalId As Integer, ByVal ignoreExpiring As Boolean) As UserValidStatus
            Dim validStatus As UserValidStatus = UserValidStatus.VALID

            'Check if Password needs to be updated
            If objUser.Membership.UpdatePassword Then
                'Admin has forced password update
                validStatus = UserValidStatus.UPDATEPASSWORD
            ElseIf PasswordConfig.PasswordExpiry > 0 Then
                Dim expiryDate As DateTime = objUser.Membership.LastPasswordChangeDate.AddDays(PasswordConfig.PasswordExpiry)
                If expiryDate < Today Then
                    'Password Expired
                    validStatus = UserValidStatus.PASSWORDEXPIRED
                ElseIf expiryDate < Today.AddDays(PasswordConfig.PasswordExpiryReminder) And (Not ignoreExpiring) Then
                    'Password update reminder
                    validStatus = UserValidStatus.PASSWORDEXPIRING
                End If
            End If
            'Check if Profile needs updating
            If validStatus = UserValidStatus.VALID Then
                Dim validProfile As Boolean = CType(UserModuleBase.GetSetting(portalId, "Security_RequireValidProfileAtLogin"), Boolean)
                If validProfile And (Not ProfileController.ValidateProfile(portalId, objUser.Profile)) Then
                    validStatus = UserValidStatus.UPDATEPROFILE
                End If
            End If

            Return validStatus
        End Function

#End Region

#Region "Private Members"

        Private _PortalId As Integer
        Private _DisplayName As String

#End Region

#Region "Public Properties"

        Public Property DisplayFormat() As String
            Get
                Return _DisplayName
            End Get
            Set(ByVal value As String)
                _DisplayName = value
            End Set
        End Property

        Public Property PortalId() As Integer
            Get
                Return _PortalId
            End Get
            Set(ByVal value As Integer)
                _PortalId = value
            End Set
        End Property

#End Region

#Region "Public Methods"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetUser retrieves a User from the DataStore
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <param name="portalId">The Id of the Portal</param>
        ''' <param name="userId">The Id of the user being retrieved from the Data Store.</param>
        ''' <returns>The User as a UserInfo object</returns>
        ''' <history>
        '''     [cnurse]	7/11/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Function GetUser(ByVal portalId As Integer, ByVal userId As Integer) As UserInfo

            Return memberProvider.GetUser(portalId, userId, False)

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Update all the Users Display Names
        ''' </summary>
        ''' <history>
        ''' 	[cnurse]	21/02/2007	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub UpdateDisplayNames()

            Dim arrUsers As ArrayList = GetUsers(PortalId)

            For Each objUser As UserInfo In arrUsers
                objUser.UpdateDisplayName(DisplayFormat)
                UpdateUser(PortalId, objUser)
            Next

        End Sub

#End Region

#Region "Obsoleted Methods, retained for Binary Compatability"

        <Obsolete("This function has been replaced by UserController.CreateUser")> _
        Public Function AddUser(ByVal objUser As UserInfo) As Integer
            Dim createStatus As UserCreateStatus = CreateUser(objUser)
            Return objUser.UserID
        End Function

        <Obsolete("This function has been replaced by UserController.CreateUser")> _
        Public Function AddUser(ByVal objUser As UserInfo, ByVal AddToMembershipProvider As Boolean) As Integer
            Dim createStatus As UserCreateStatus = CreateUser(objUser)
            Return objUser.UserID
        End Function

        <Obsolete("This function has been replaced by UserController.DeleteUsers")> _
        Public Sub DeleteAllUsers(ByVal PortalId As Integer)
            DeleteUsers(PortalId, False, True)
        End Sub

        <Obsolete("This function has been replaced by UserController.DeleteUser")> _
        Public Function DeleteUser(ByVal PortalId As Integer, ByVal UserId As Integer) As Boolean
            Dim objUser As UserInfo = GetUser(PortalId, UserId, False)

            'Call Shared method with notify=true, deleteAdmin=false
            Return DeleteUser(objUser, True, False)
        End Function

        <Obsolete("This function has been replaced by UserController.DeleteUsers")> _
        Public Sub DeleteUsers(ByVal PortalId As Integer)
            DeleteUsers(PortalId, True, False)
        End Sub

        <Obsolete("This function has been replaced by UserController.GetUserByName")> _
        Public Function FillUserInfo(ByVal PortalID As Integer, ByVal Username As String) As UserInfo
            Return GetUserByName(PortalID, Username, False)
        End Function

        <Obsolete("This function should be replaced by String.Format(DataCache.UserCacheKey, portalId, username)")> _
        Public Function GetCacheKey(ByVal PortalID As Integer, ByVal Username As String) As String
            Return String.Format(DataCache.UserCacheKey, PortalID, Username)
        End Function

        <Obsolete("This function should be replaced by String.Format(DataCache.UserCacheKey, portalId, username)")> _
        Public Shared Function CacheKey(ByVal portalId As Integer, ByVal username As String) As String
            Return String.Format(DataCache.UserCacheKey, portalId, username)
        End Function


        <Obsolete("This function has been replaced by UserController.GetUserByName")> _
        Public Function GetUserByUsername(ByVal PortalID As Integer, ByVal Username As String) As UserInfo
            Return GetUserByName(portalId, username, False)
        End Function

        <Obsolete("This function has been replaced by UserController.GetUserByName")> _
        Public Function GetUserByUsername(ByVal PortalID As Integer, ByVal Username As String, ByVal SynchronizeUsers As Boolean) As UserInfo
            Return GetUserByName(PortalID, Username, False)
        End Function

        <Obsolete("This function has been replaced by UserController.GetUsers")> _
        Public Function GetSuperUsers() As ArrayList
            Return GetUsers(Null.NullInteger, False)
        End Function

        <Obsolete("This function has been replaced by UserController.GetUsers")> _
        Public Function GetUsers(ByVal SynchronizeUsers As Boolean, ByVal ProgressiveHydration As Boolean) As ArrayList
            Return GetUsers(Null.NullInteger, ProgressiveHydration)
        End Function

        <Obsolete("This function has been replaced by UserController.GetUsers")> _
        Public Function GetUsers(ByVal PortalId As Integer, ByVal SynchronizeUsers As Boolean, ByVal ProgressiveHydration As Boolean) As ArrayList
            Return GetUsers(PortalId, ProgressiveHydration)
        End Function

        <Obsolete("This function has been replaced by UserController.ChangePassword")> _
        Public Function SetPassword(ByVal objUser As UserInfo, ByVal newPassword As String) As Boolean
            Return ChangePassword(objUser, Null.NullString, newPassword)
        End Function

        <Obsolete("This function has been replaced by UserController.ChangePassword")> _
        Public Function SetPassword(ByVal objUser As UserInfo, ByVal oldPassword As String, ByVal newPassword As String) As Boolean
            Return ChangePassword(objUser, oldPassword, newPassword)
        End Function

        <Obsolete("This function has been replaced by UserController.UnlockUserAccount")> _
        Public Sub UnlockUserAccount(ByVal objUser As UserInfo)
            UnLockUser(objUser)
        End Sub

        <Obsolete("This function has been replaced by UserController.UpdateUser")> _
        Public Sub UpdateUser(ByVal objUser As UserInfo)
            UpdateUser(objUser.PortalID, objUser)
        End Sub



#End Region

    End Class


End Namespace
