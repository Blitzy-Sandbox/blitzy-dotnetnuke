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

Imports System.IO
Imports System.Xml.XPath

Namespace DotNetNuke.Services.Packages

    ''' -----------------------------------------------------------------------------
    ''' <summary>
    ''' The InstallComponent class represents a component of a Installer Package
    ''' </summary>
    ''' <remarks>
    ''' </remarks>
    ''' <history>
    ''' 	[cnurse]	07/24/2007  created
    ''' </history>
    ''' -----------------------------------------------------------------------------
    Public Class ComponentInfo

#Region "Private Members"

        Private _Installer As ComponentInstallerBase
        Private _Log As Logger
        Private _Type As String

#End Region

#Region "Constructors"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' This Constructor creates a new InstallComponent instance
        ''' </summary>
        ''' <history>
        ''' 	[cnurse]	07/24/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub New(ByVal type As String, ByVal manifestNav As XPathNavigator, ByVal package As PackageInfo)
            _Type = type
            _Log = package.Log
            _Installer = InstallerFactory.GetInstaller(manifestNav, package)
            If _Installer Is Nothing Then
                Log.AddFailure(Util.EXCEPTION_InstallerCreate)
            End If
        End Sub

#End Region

#Region "Public Properties"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the Completed flag
        ''' </summary>
        ''' <value>A Boolean value</value>
        ''' <history>
        ''' 	[cnurse]	07/31/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public ReadOnly Property Completed() As Boolean
            Get
                Return Installer.Completed
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the associated Installer
        ''' </summary>
        ''' <value>A ComponentInstallerBase</value>
        ''' <history>
        ''' 	[cnurse]	07/24/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public ReadOnly Property Installer() As ComponentInstallerBase
            Get
                Return _Installer
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the Logger
        ''' </summary>
        ''' <value>An Logger object</value>
        ''' <history>
        ''' 	[cnurse]	07/31/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public ReadOnly Property Log() As Logger
            Get
                Return _Log
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the Type of this Component
        ''' </summary>
        ''' <value>A String</value>
        ''' <history>
        ''' 	[cnurse]	07/24/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public ReadOnly Property Type() As String
            Get
                Return _Type
            End Get
        End Property

#End Region

#Region "Public Methods"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The Commit method commits the component installation
        ''' </summary>
        ''' <history>
        ''' 	[cnurse]	08/01/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub Commit()
            Installer.Commit()
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The Install method installs the component
        ''' </summary>
        ''' <history>
        ''' 	[cnurse]	07/25/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub Install()
            Log.AddInfo(Util.INSTALL_Start + " - " + Type)
            Installer.Install()
            If Completed Then
                Log.AddInfo(Util.COMPONENT_Installed + " - " + Type)
            Else
                Log.AddFailure(Util.INSTALL_Failed + " - " + Type)
            End If
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The Rollback method rolls back the component installation
        ''' </summary>
        ''' <history>
        ''' 	[cnurse]	07/31/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub Rollback()
            Log.AddInfo(Util.COMPONENT_RollingBack + " - " + Type)
            Installer.Rollback()
            Log.AddInfo(Util.COMPONENT_RolledBack + " - " + Type)
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The UnInstall method removes the component
        ''' </summary>
        ''' <history>
        ''' 	[cnurse]	07/25/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub UnInstall()
            Log.AddInfo(Util.UNINSTALL_StartComp + " - " + Type)
            Installer.UnInstall()
            Log.AddInfo(Util.COMPONENT_UnInstalled + " - " + Type)
        End Sub

#End Region

    End Class

End Namespace
