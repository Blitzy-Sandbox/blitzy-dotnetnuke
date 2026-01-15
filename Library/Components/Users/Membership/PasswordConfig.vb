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

Imports DotNetNuke.UI.WebControls

Namespace DotNetNuke.Security.Membership

    ''' -----------------------------------------------------------------------------
    ''' Project:    DotNetNuke
    ''' Namespace:  DotNetNuke.Security.Membership
    ''' Class:      PasswordConfig
    ''' -----------------------------------------------------------------------------
    ''' <summary>
    ''' The PasswordConfig class provides a wrapper any Portal wide Password Settings
    ''' </summary>
    ''' <remarks>
    ''' </remarks>
    ''' <history>
    '''     [cnurse]	03/02/2006	created
    ''' </history>
    ''' -----------------------------------------------------------------------------
    Public Class PasswordConfig

#Region "Public Shared Properties"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets and sets the Password Expiry time in days
        ''' </summary>
        ''' <returns>An integer.</returns>
        ''' <history>
        '''     [cnurse]	03/02/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        <SortOrder(0), Category("Password")> Public Shared Property PasswordExpiry() As Integer
            Get
                Dim _portalSettings As PortalSettings = PortalController.GetCurrentPortalSettings()
                Dim _PasswordExpiry As Integer = 0
                If Not _portalSettings.HostSettings("PasswordExpiry") Is Nothing Then
                    _PasswordExpiry = CType(_portalSettings.HostSettings("PasswordExpiry"), Integer)
                End If
                Return _PasswordExpiry
            End Get
            Set(ByVal Value As Integer)
                Dim objHostSettings As New Entities.Host.HostSettingsController
                objHostSettings.UpdateHostSetting("PasswordExpiry", Value.ToString)
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets and sets the a Reminder time in days (to remind the user that theire password 
        ''' is about to expire
        ''' </summary>
        ''' <returns>An integer.</returns>
        ''' <history>
        '''     [cnurse]	03/02/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        <SortOrder(1), Category("Password")> Public Shared Property PasswordExpiryReminder() As Integer
            Get
                Dim _portalSettings As PortalSettings = PortalController.GetCurrentPortalSettings()
                Dim _PasswordExpiry As Integer = 7
                If Not _portalSettings.HostSettings("PasswordExpiryReminder ") Is Nothing Then
                    _PasswordExpiry = CType(_portalSettings.HostSettings("PasswordExpiryReminder "), Integer)
                End If
                Return _PasswordExpiry
            End Get
            Set(ByVal Value As Integer)
                Dim objHostSettings As New Entities.Host.HostSettingsController
                objHostSettings.UpdateHostSetting("PasswordExpiryReminder ", Value.ToString)
            End Set
        End Property

#End Region

    End Class


End Namespace
