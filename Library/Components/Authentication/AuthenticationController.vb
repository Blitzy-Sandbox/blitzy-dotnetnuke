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
Imports System.Globalization
Imports System.Windows.Forms

Imports DotNetNuke.Entities.Modules

Namespace DotNetNuke.Services.Authentication

    ''' -----------------------------------------------------------------------------
    ''' <summary>
    ''' The AuthenticationController class provides the Business Layer for the 
    ''' Authentication Systems.
    ''' </summary>
    ''' <history>
    ''' 	[cnurse]	07/10/2007  Created
    ''' </history>
    ''' -----------------------------------------------------------------------------
    Public Class AuthenticationController

#Region "Private Members"

        Private Shared provider As DataProvider = DataProvider.Instance()

#End Region

#Region "Private Shared Methods"

        Private Shared Function FillInfo(ByVal dr As IDataReader) As AuthenticationInfo
            Dim objAuthenticationInfo As AuthenticationInfo = Nothing

            Try
                objAuthenticationInfo = FillInfo(dr, True)
            Finally
                If Not dr Is Nothing Then
                    dr.Close()
                End If
            End Try

            Return objAuthenticationInfo
        End Function

        Private Shared Function FillInfo(ByVal dr As IDataReader, ByVal CheckForOpenDataReader As Boolean) As AuthenticationInfo
            Dim objAuthenticationInfo As New AuthenticationInfo

            ' read datareader
            Dim canContinue As Boolean = True
            If CheckForOpenDataReader Then
                canContinue = False
                If dr.Read Then
                    canContinue = True
                End If
            End If
            If canContinue Then
                objAuthenticationInfo.AuthenticationID = Convert.ToInt32(Null.SetNull(dr("AuthenticationID"), objAuthenticationInfo.AuthenticationID))
                objAuthenticationInfo.PackageID = Convert.ToInt32(Null.SetNull(dr("PackageID"), objAuthenticationInfo.PackageID))
                objAuthenticationInfo.IsEnabled = Convert.ToBoolean(Null.SetNull(dr("IsEnabled"), objAuthenticationInfo.IsEnabled))
                objAuthenticationInfo.AuthenticationType = Convert.ToString(Null.SetNull(dr("AuthenticationType"), objAuthenticationInfo.AuthenticationType))
                objAuthenticationInfo.SettingsControlSrc = Convert.ToString(Null.SetNull(dr("SettingsControlSrc"), objAuthenticationInfo.SettingsControlSrc))
                objAuthenticationInfo.LoginControlSrc = Convert.ToString(Null.SetNull(dr("LoginControlSrc"), objAuthenticationInfo.LoginControlSrc))
                objAuthenticationInfo.LogoffControlSrc = Convert.ToString(Null.SetNull(dr("LogoffControlSrc"), objAuthenticationInfo.LogoffControlSrc))
            Else
                objAuthenticationInfo = Nothing
            End If

            Return objAuthenticationInfo
        End Function

        Private Shared Function FillList(ByVal dr As IDataReader) As List(Of AuthenticationInfo)
            Dim arr As New List(Of AuthenticationInfo)
            Try
                Dim obj As AuthenticationInfo
                While dr.Read
                    ' fill business object
                    obj = FillInfo(dr, False)
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

#End Region

#Region "Public Shared Methods"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' AddAuthentication adds a new Authentication System to the Data Store.
        ''' </summary>
        ''' <param name="authSystem">The new Authentication System to add</param>
        ''' <history>
        ''' 	[cnurse]	07/10/2007  Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function AddAuthentication(ByVal authSystem As AuthenticationInfo) As Integer
            Return provider.AddAuthentication(authSystem.PackageID, authSystem.AuthenticationType, authSystem.IsEnabled, authSystem.SettingsControlSrc, authSystem.LoginControlSrc, authSystem.LogoffControlSrc)
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' AddUserAuthentication adds a new UserAuthentication to the User.
        ''' </summary>
        ''' <param name="userID">The new Authentication System to add</param>
        ''' <param name="authenticationType">The authentication type</param>
        ''' <param name="authenticationToken">The authentication token</param>
        ''' <history>
        ''' 	[cnurse]	07/12/2007  Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function AddUserAuthentication(ByVal userID As Integer, ByVal authenticationType As String, ByVal authenticationToken As String) As Integer
            Return provider.AddUserAuthentication(userID, authenticationType, authenticationToken)
        End Function

        Public Shared Sub DeleteAuthentication(ByVal authSystem As AuthenticationInfo)
            provider.DeleteAuthentication(authSystem.AuthenticationID)
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetAuthenticationService fetches a single Authentication Systems 
        ''' </summary>
        ''' <param name="authenticationID">The ID of the Authentication System</param>
        ''' <returns>An AuthenticationInfo object</returns>
        ''' <history>
        ''' 	[cnurse]	07/31/2007  Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetAuthenticationService(ByVal authenticationID As Integer) As AuthenticationInfo
            Return FillInfo(provider.GetAuthenticationService(authenticationID))
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetAuthenticationServiceByPackageID fetches a single Authentication System 
        ''' </summary>
        ''' <param name="packageID">The id of the Package</param>
        ''' <returns>An AuthenticationInfo object</returns>
        ''' <history>
        ''' 	[cnurse]	07/31/2007  Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetAuthenticationServiceByPackageID(ByVal packageID As Integer) As AuthenticationInfo
            Return FillInfo(provider.GetAuthenticationServiceByPackageID(packageID))
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetAuthenticationServiceByType fetches a single Authentication Systems 
        ''' </summary>
        ''' <param name="authenticationType">The type of the Authentication System</param>
        ''' <returns>An AuthenticationInfo object</returns>
        ''' <history>
        ''' 	[cnurse]	07/31/2007  Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetAuthenticationServiceByType(ByVal authenticationType As String) As AuthenticationInfo
            Return FillInfo(provider.GetAuthenticationServiceByType(authenticationType))
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetAuthenticationServices fetches a list of all the Authentication Systems 
        ''' installed in the system
        ''' </summary>
        ''' <returns>A List of AuthenticationInfo objects</returns>
        ''' <history>
        ''' 	[cnurse]	07/10/2007  Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetAuthenticationServices() As List(Of AuthenticationInfo)
            Return FillList(provider.GetAuthenticationServices())
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetAuthenticationType fetches the authentication method used by the currently logged on user
        ''' </summary>
        ''' <returns>An AuthenticationInfo object</returns>
        ''' <history>
        ''' 	[cnurse]	07/23/2007  Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetAuthenticationType() As AuthenticationInfo
            Dim objAuthentication As AuthenticationInfo = Nothing
            If HttpContext.Current IsNot Nothing AndAlso HttpContext.Current.Request IsNot Nothing Then
                Try
                    objAuthentication = GetAuthenticationServiceByType(HttpContext.Current.Request("authentication"))
                Catch
                End Try
            End If

            Return objAuthentication
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetEnabledAuthenticationServices fetches a list of all the Authentication Systems 
        ''' installed in the system that have been enabled by the Host user
        ''' </summary>
        ''' <returns>A List of AuthenticationInfo objects</returns>
        ''' <history>
        ''' 	[cnurse]	07/10/2007  Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetEnabledAuthenticationServices() As List(Of AuthenticationInfo)
            Return FillList(provider.GetEnabledAuthenticationServices())
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetLogoffRedirectURL fetches the url to redirect too after logoff
        ''' </summary>
        ''' <param name="settings">A PortalSettings object</param>
        ''' <param name="request">The current Request</param>
        ''' <returns>The Url</returns>
        ''' <history>
        ''' 	[cnurse]	08/15/2007  Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetLogoffRedirectURL(ByVal settings As PortalSettings, ByVal request As HttpRequest) As String
            Dim _RedirectURL As String = ""

            Dim setting As Object = UserModuleBase.GetSetting(settings.PortalId, "Redirect_AfterLogout")

            If CType(setting, Integer) = Null.NullInteger Then
                If settings.ActiveTab.AuthorizedRoles.IndexOf(";" & glbRoleAllUsersName & ";") <> -1 Then
                    ' redirect to current page
                    _RedirectURL = NavigateURL(settings.ActiveTab.TabID)
                ElseIf settings.HomeTabId <> -1 Then
                    ' redirect to portal home page specified
                    _RedirectURL = NavigateURL(settings.HomeTabId)
                Else ' redirect to default portal root
                    _RedirectURL = GetPortalDomainName(settings.PortalAlias.HTTPAlias, request) & "/" & glbDefaultPage
                End If
            Else ' redirect to after logout page
                _RedirectURL = NavigateURL(CType(setting, Integer))
            End If

                Return _RedirectURL
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' SetAuthenticationType sets the authentication method used by the currently logged on user
        ''' </summary>
        ''' <param name="value">The Authentication type</param>
        ''' <history>
        ''' 	[cnurse]	07/23/2007  Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub SetAuthenticationType(ByVal value As String)
            Try
                Dim Response As HttpResponse = HttpContext.Current.Response
                If Response Is Nothing Then
                    Return
                End If

                ' save the authenticationmethod as a cookie
                Dim cookie As System.Web.HttpCookie = Nothing
                cookie = Response.Cookies.Get("authentication")
                If (cookie Is Nothing) Then
                    If value <> "" Then
                        cookie = New System.Web.HttpCookie("authentication", value)
                        Response.Cookies.Add(cookie)
                    End If
                Else
                    cookie.Value = value
                    If value <> "" Then
                        Response.Cookies.Set(cookie)
                    Else
                        Response.Cookies.Remove("authentication")
                    End If
                End If
            Catch
                Return
            End Try
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' UpdateAuthentication updates an existing Authentication System in the Data Store.
        ''' </summary>
        ''' <param name="authSystem">The new Authentication System to update</param>
        ''' <history>
        ''' 	[cnurse]	07/10/2007  Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Sub UpdateAuthentication(ByVal authSystem As AuthenticationInfo)
            provider.UpdateAuthentication(authSystem.AuthenticationID, authSystem.PackageID, authSystem.AuthenticationType, authSystem.IsEnabled, authSystem.SettingsControlSrc, authSystem.LoginControlSrc, authSystem.LogoffControlSrc)
        End Sub

#End Region

    End Class

End Namespace
