'
' DotNetNuke® - http://www.dotnetnuke.com
' Copyright (c) 2002-2008 by DotNetNuke Corp. 
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

Imports DotNetNuke
Imports DotNetNuke.Entities.Host.HostSettings
Imports DotNetNuke.Entities.Modules
Imports DotNetNuke.Entities.Portals
Imports DotNetNuke.Entities.Tabs
Imports DotNetNuke.Entities.Users
Imports DotNetNuke.Entities.Profile
Imports DotNetNuke.Services.Localization
Imports System.Globalization
Imports System.Web
Imports System.Text
Imports System.Text.RegularExpressions


Namespace DotNetNuke.Services.Localization

#Region " Type Definitions "
    Public Enum TokenAccessLevel
        NoSettings
        DefaultSettings
        FullSettings
    End Enum
#End Region

    ''' -----------------------------------------------------------------------------
    ''' Project:    DotNetNuke
    ''' Namespace:  DotNetNuke.Services.Localization
    ''' Class:      TokenReplace
    ''' -----------------------------------------------------------------------------
    ''' <summary>
    ''' The TokenReplace class provides the option to replace tokens formatted 
    ''' [object:property] or [object:property|format] or [custom:no] within a string
    ''' with the appropriate current property/custom values.
    ''' Example for Newsletter: 'Dear [user:Displayname],' ==> 'Dear Superuser Account,'
    ''' </summary>
    ''' -----------------------------------------------------------------------------
    <Obsolete("This class has been deprecated, please use DotNetNuke.Services.Tokens.TokenReplace instead")> _
    Public Class TokenReplace

        Private _TokenReplace As DotNetNuke.Services.Tokens.TokenReplace


#Region " Properties "
        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets/sets the language to be used, e.g. for date format
        ''' </summary>
        ''' <value>A string, representing the locale</value>
        ''' <history>
        ''' 	[scullmann]	01/04/2007	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Property Language() As String
            Get
                Return _TokenReplace.Language
            End Get
            Set(ByVal value As String)
                _TokenReplace.Language = value
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets/sets the portal settings object to use for 'Portal:' token replacement
        ''' </summary>
        ''' <value>PortalSettings oject</value>
        ''' <history>
        ''' 	[scullmann]	01/04/2007	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Property PortalSettings() As PortalSettings
            Get
                Return _TokenReplace.PortalSettings
            End Get
            Set(ByVal value As PortalSettings)
                _TokenReplace.PortalSettings = value
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets/sets the module settings object to use for 'Module:' token replacement
        ''' </summary>
        ''' <value>ModuleInfo oject</value>
        ''' <history>
        ''' 	[scullmann]	01/04/2007	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Property ModuleInfo() As Entities.Modules.ModuleInfo
            Get

                Return _TokenReplace.ModuleInfo
            End Get
            Set(ByVal value As Entities.Modules.ModuleInfo)
                _TokenReplace.ModuleInfo = value
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets/sets the user object to use for 'User:' token replacement
        ''' </summary>
        ''' <value>UserInfo oject</value>
        ''' <history>
        ''' 	[scullmann]	01/04/2007	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Property User() As Entities.Users.UserInfo
            Get
                Return _TokenReplace.User
            End Get
            Set(ByVal value As Entities.Users.UserInfo)
                _TokenReplace.User = value
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets/sets the user object representing the currently accessing user (permission)
        ''' </summary>
        ''' <value>UserInfo oject</value>
        ''' <history>
        ''' 	[scullmann]	01/04/2007	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Property AccessingUser() As Entities.Users.UserInfo
            Get
                Return _TokenReplace.AccessingUser
            End Get
            Set(ByVal value As Entities.Users.UserInfo)
                _TokenReplace.AccessingUser = value
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets/sets the current ModuleID to be used for 'User:' token replacement
        ''' </summary>
        ''' <value>ModuleID (Integer)</value>
        ''' <history>
        ''' 	[scullmann]	01/04/2007	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Property ModuleId() As Integer
            Get
                Return _TokenReplace.ModuleId
            End Get
            Set(ByVal value As Integer)

                _TokenReplace.ModuleId = value
            End Set
        End Property

#End Region

#Region " Constructor and fabric methods "

        Private Sub New(ByVal AccessLevel As TokenAccessLevel, ByVal Language As String, ByVal PortalSettings As PortalSettings, ByVal User As UserInfo)
            Dim level As DotNetNuke.Services.Tokens.Scope
            Select Case AccessLevel
                Case TokenAccessLevel.DefaultSettings
                    level = Tokens.Scope.DefaultSettings
                Case TokenAccessLevel.NoSettings
                    level = Tokens.Scope.NoSettings
                Case TokenAccessLevel.FullSettings
                    level = Tokens.Scope.SystemMessages
            End Select
            _TokenReplace = New DotNetNuke.Services.Tokens.TokenReplace(level, Language, PortalSettings, User)
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Create the object, basing on different passed objects. Missing object will be
        ''' replaced by objects created from current context
        ''' </summary>
        ''' <returns>TokenReplace Object</returns>
        ''' <param name="ModuleID">ID of the Module to use</param>
        ''' <history>
        ''' 	[scullmann]	01/04/2007	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function Create(ByVal ModuleID As Integer) As TokenReplace
            Dim tr As TokenReplace = Create(TokenAccessLevel.DefaultSettings)
            tr.ModuleId = ModuleID
            Return tr
        End Function

        Public Shared Function Create(ByVal objModuleInfo As ModuleInfo) As TokenReplace
            Dim tr As TokenReplace = Create(TokenAccessLevel.DefaultSettings)
            tr.ModuleId = objModuleInfo.ModuleID
            tr.ModuleInfo = objModuleInfo
            Return tr
        End Function

        Public Shared Function Create() As TokenReplace
            Return Create(TokenAccessLevel.DefaultSettings)
        End Function

        Public Shared Function Create(ByVal Language As String) As TokenReplace
            Return Create(TokenAccessLevel.DefaultSettings, Language, Nothing, Nothing)
        End Function

        Public Shared Function Create(ByVal AccessLevel As TokenAccessLevel) As TokenReplace
            Return Create(AccessLevel, Nothing, Nothing, Nothing)
        End Function

        Public Shared Function Create(ByVal AccessLevel As TokenAccessLevel, ByVal Language As String) As TokenReplace
            Return Create(AccessLevel, Language, Nothing, Nothing)
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Create the object, basing on different passed objects. Missing object will be
        ''' replaced by objects created from current context
        ''' </summary>
        ''' <returns>TokenReplace Object</returns>
        ''' <param name="AccessLevel">maximum Security Access level for private properties, limited by accessing user's permissions</param>
        ''' <param name="Language">Locale to be used for formatting</param>
        ''' <param name="PortalSettings">Portal to be queried for portal and admin settings</param>
        ''' <param name="User">User object to read user data and profile proerties from</param>
        ''' <history>
        ''' 	[scullmann]	01/04/2007	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function Create(ByVal AccessLevel As TokenAccessLevel, ByVal Language As String, ByVal PortalSettings As PortalSettings, ByVal User As UserInfo) As TokenReplace
            Return New TokenReplace(AccessLevel, Language, PortalSettings, User)
        End Function

#End Region

#Region " Public Methods "

        Public Function ReplaceEnvironmentTokens(ByVal strSourceText As String) As String
            Return _TokenReplace.ReplaceEnvironmentTokens(strSourceText)
        End Function

        Public Function ReplaceEnvironmentTokens(ByVal strSourceText As String, ByVal row As DataRow) As String
            Return _TokenReplace.ReplaceEnvironmentTokens(strSourceText, row)
        End Function

        Public Function ReplaceEnvironmentTokens(ByVal strSourceText As String, ByVal Custom As ArrayList, ByVal CustomCaption As String) As String
            Return _TokenReplace.ReplaceEnvironmentTokens(strSourceText, Custom, CustomCaption)
        End Function

        Public Function ReplaceEnvironmentTokens(ByVal strSourceText As String, ByVal Custom As ArrayList, ByVal CustomCaption As String, ByVal Row As System.Data.DataRow) As String
            Return _TokenReplace.ReplaceEnvironmentTokens(strSourceText, Custom, CustomCaption, Row)
        End Function

#End Region

#Region " Obsolete Methods "
        <Obsolete("This property has been deprecated, use overload with additional CustomCaption parameter")> _
        Public Function ReplaceEnvironmentTokens(ByVal strSourceText As String, ByVal Custom As ArrayList) As String
            Return ReplaceEnvironmentTokens(strSourceText, Custom, "Custom", Nothing)
        End Function

        <Obsolete("This property has been deprecated, use overload with additional CustomCaption parameter")> _
        Public Function ReplaceEnvironmentTokens(ByVal strSourceText As String, ByVal Custom As ArrayList, ByVal Row As System.Data.DataRow) As String
            Return ReplaceEnvironmentTokens(strSourceText, Custom, "Custom", Row)
        End Function
#End Region
    End Class
End Namespace
