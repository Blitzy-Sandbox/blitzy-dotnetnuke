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

Imports System.Collections.Generic
Imports System.ComponentModel
Imports System.IO
Imports System.Text.RegularExpressions
Imports System.Xml.XPath

Imports DotNetNuke.UI.WebControls

Namespace DotNetNuke.Services.Packages

    ''' -----------------------------------------------------------------------------
    ''' <summary>
    ''' The PackageInfo class represents a single Installer Package
    ''' </summary>
    ''' <remarks>
    ''' </remarks>
    ''' <history>
    ''' 	[cnurse]	07/24/2007  created
    ''' </history>
    ''' -----------------------------------------------------------------------------
    Public Class PackageInfo

#Region "Private Members"

        Private _PackageID As Integer = Null.NullInteger
        Private _Components As New SortedList(Of Integer, ComponentInfo)
        Private _Description As String
        Private _FriendlyName As String
        Private _IsValid As Boolean = True
        Private _InstalledVersion As String = "00.00.00"
        Private _InstallerInfo As InstallerInfo
        Private _License As String
        Private _Manifest As String
        Private _Name As String
        Private _PackageType As String
        Private _MajorVersion As Integer
        Private _MinorVersion As Integer
        Private _Revision As Integer
        Private _Version As String

#End Region

#Region "Constructors"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' This Constructor creates a new InstallPackage instance as defined by the
        ''' Parameters
        ''' </summary>
        ''' <history>
        ''' 	[cnurse]	07/24/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub New(ByVal packageManifest As String, ByVal info As InstallerInfo)
            _InstallerInfo = info
            _Manifest = packageManifest

            ReadManifest()
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' This Constructor creates a new InstallPackage instance
        ''' </summary>
        ''' <history>
        ''' 	[cnurse]	07/24/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub New()
        End Sub

#End Region

#Region "Public Properties"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the ID of this package
        ''' </summary>
        ''' <value>An Integer</value>
        ''' <history>
        ''' 	[cnurse]	07/26/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        <Browsable(False)> _
        Public Property PackageID() As Integer
            Get
                Return _PackageID
            End Get
            Set(ByVal value As Integer)
                _PackageID = value
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets a sorted list of Components for this package
        ''' </summary>
        ''' <value>A SortedList(Of Integer, ComponentInfo)</value>
        ''' <history>
        ''' 	[cnurse]	07/24/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        <Browsable(False)> _
        Public ReadOnly Property Components() As SortedList(Of Integer, ComponentInfo)
            Get
                Return _Components
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the Description of this package
        ''' </summary>
        ''' <value>A String</value>
        ''' <history>
        ''' 	[cnurse]	07/26/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        <SortOrder(3)> _
        Public Property Description() As String
            Get
                Return _Description
            End Get
            Set(ByVal value As String)
                _Description = value
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets a Dictionary of Files that are included in the Package
        ''' </summary>
        ''' <value>A Dictionary(Of String, InstallFile)</value>
        ''' <history>
        ''' 	[cnurse]	07/31/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        <Browsable(False)> _
        Public ReadOnly Property Files() As Dictionary(Of String, InstallFile)
            Get
                Return InstallerInfo.Files
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets and Sets the FriendlyName of this package
        ''' </summary>
        ''' <value>A String</value>
        ''' <history>
        ''' 	[cnurse]	08/03/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        <SortOrder(2)> _
        Public Property FriendlyName() As String
            Get
                Return _FriendlyName
            End Get
            Set(ByVal value As String)
                _FriendlyName = value
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the associated InstallerInfo
        ''' </summary>
        ''' <value>An InstallerInfo object</value>
        ''' <history>
        ''' 	[cnurse]	07/24/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        <Browsable(False)> _
        Public ReadOnly Property InstallerInfo() As InstallerInfo
            Get
                Return _InstallerInfo
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the InstalledVersion of the Package
        ''' </summary>
        ''' <value>A String</value>
        ''' <history>
        ''' 	[cnurse]	08/07/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        <Browsable(False)> _
        Public ReadOnly Property InstalledVersion() As String
            Get
                Return _InstalledVersion
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the InstallMode
        ''' </summary>
        ''' <value>An InstallMode value</value>
        ''' <history>
        ''' 	[cnurse]	07/31/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        <Browsable(False)> _
        Public ReadOnly Property InstallMode() As InstallMode
            Get
                Return InstallerInfo.InstallMode
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets whether the Package is Valid
        ''' </summary>
        ''' <value>A Boolean value</value>
        ''' <history>
        ''' 	[cnurse]	08/03/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        <Browsable(False)> _
        Public ReadOnly Property IsValid() As Boolean
            Get
                Return _IsValid
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets and Sets the License of this package
        ''' </summary>
        ''' <value>A String</value>
        ''' <history>
        ''' 	[cnurse]	08/03/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        <SortOrder(5), _
        Editor("DotNetNuke.UI.WebControls.DNNRichTextEditControl", "DotNetNuke.UI.WebControls.EditControl")> _
        Public Property License() As String
            Get
                Return _License
            End Get
            Set(ByVal value As String)
                _License = value
            End Set
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
        <Browsable(False)> _
        Public ReadOnly Property Log() As Logger
            Get
                Return InstallerInfo.Log
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the Major Version of this package
        ''' </summary>
        ''' <value>An INteger</value>
        ''' <history>
        ''' 	[cnurse]	07/24/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        <Browsable(False)> _
        Public ReadOnly Property MajorVersion() As Integer
            Get
                Return _MajorVersion
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the Minor Version of this package
        ''' </summary>
        ''' <value>An INteger</value>
        ''' <history>
        ''' 	[cnurse]	07/24/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        <Browsable(False)> _
        Public ReadOnly Property MinorVersion() As Integer
            Get
                Return _MinorVersion
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the Manifest of this package
        ''' </summary>
        ''' <value>A String</value>
        ''' <history>
        ''' 	[cnurse]	07/26/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        <Browsable(False)> _
        Public Property Manifest() As String
            Get
                Return _Manifest
            End Get
            Set(ByVal value As String)
                _Manifest = value
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the Name of this package
        ''' </summary>
        ''' <value>A String</value>
        ''' <history>
        ''' 	[cnurse]	07/24/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        ''' 
        <SortOrder(0)> _
        Public Property Name() As String
            Get
                Return _Name
            End Get
            Set(ByVal value As String)
                _Name = value
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the Revision Version of this package
        ''' </summary>
        ''' <value>An INteger</value>
        ''' <history>
        ''' 	[cnurse]	07/24/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        <Browsable(False)> _
        Public ReadOnly Property Revision() As Integer
            Get
                Return _Revision
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the Type of this package
        ''' </summary>
        ''' <value>A String</value>
        ''' <history>
        ''' 	[cnurse]	07/24/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        <SortOrder(1)> _
        Public Property PackageType() As String
            Get
                Return _PackageType
            End Get
            Set(ByVal value As String)
                _PackageType = value
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the Manifest of this package
        ''' </summary>
        ''' <value>A String</value>
        ''' <history>
        ''' 	[cnurse]	07/26/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        <SortOrder(4)> _
        Public Property Version() As String
            Get
                Return _Version
            End Get
            Set(ByVal value As String)
                _Version = value
                GetVersion()
            End Set
        End Property

#End Region

#Region "Private Methods"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The CheckSecurity method checks whether the user has the appropriate security
        ''' </summary>
        ''' <history>
        ''' 	[cnurse]	09/04/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub CheckSecurity()
            Dim type As PackageType = PackageController.GetPackageType(PackageType)

            If type Is Nothing Then
                'This package type not registered
                Log.Logs.Clear()
                Log.AddFailure(Util.SECURITY_NotRegistered + " - " + PackageType)
                _IsValid = False
            Else
                If type.SecurityAccessLevel < InstallerInfo.SecurityAccessLevel Then
                    Log.Logs.Clear()
                    Log.AddFailure(Util.SECURITY_Installer)
                    _IsValid = False
                End If

            End If

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The GetVersion method parses the string version into its components
        ''' </summary>
        ''' <history>
        ''' 	[cnurse]	07/24/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub GetVersion()
            Dim intVersion() As Integer = Upgrade.Upgrade.GetVersion(Version)
            _MajorVersion = intVersion(0)
            _MinorVersion = intVersion(1)
            _Revision = intVersion(2)
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The ValidateVersion method checks whether the package is already installed
        ''' </summary>
        ''' <history>
        ''' 	[cnurse]	07/24/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub ValidateVersion()
            'Get Package from DataStore
            Dim package As PackageInfo = PackageController.GetPackageByName(Name)

            If package IsNot Nothing Then
                _InstalledVersion = package.Version
            End If

            If Me.InstalledVersion > Me.Version Then
                InstallerInfo.Log.AddWarning(Util.INSTALL_Version + " - " + Me.InstalledVersion)
                _IsValid = False
            End If
        End Sub

#End Region

#Region "Public Methods"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The AttachInstallerInfo method attachs an InstallerInfo instance to the Package
        ''' </summary>
        ''' <param name="installer">The InstallerInfo instance to attach</param>
        ''' <history>
        ''' 	[cnurse]	08/01/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub AttachInstallerInfo(ByVal installer As InstallerInfo)
            _InstallerInfo = installer
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The Commit method commits the package installation
        ''' </summary>
        ''' <history>
        ''' 	[cnurse]	08/01/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub Commit()
            For index As Integer = 0 To Components.Count - 1
                Dim comp As ComponentInfo = Components.Values(index)
                If comp.Completed Then
                    comp.Commit()
                End If
            Next
            If Log.Valid Then
                Log.AddInfo(Util.INSTALL_Committed)
            Else
                Log.AddFailure(Util.INSTALL_Aborted)
            End If
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The Install method installs the components of the package
        ''' </summary>
        ''' <history>
        ''' 	[cnurse]	07/25/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub Install()
            Dim isCompleted As Boolean = True

            Try
                'Save the Package Information
                PackageID = PackageController.AddPackage(Me)

                'Iterate through all the Components
                For index As Integer = 0 To Components.Count - 1
                    Dim comp As ComponentInfo = Components.Values(index)
                    comp.Install()
                    If Not comp.Completed Then
                        isCompleted = False
                        Exit For
                    End If
                Next
            Catch ex As Exception
                Log.AddFailure(Util.INSTALL_Aborted + " - " + Name)
            End Try

            If isCompleted Then
                'All components successfully installed so Commit any pending changes
                Commit()
            Else
                'There has been a failure so Rollback
                Rollback()
            End If


        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The ReadManifest method reads the manifest file and parses it into components.
        ''' </summary>
        ''' <history>
        ''' 	[cnurse]	07/24/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub ReadManifest()
            If Not String.IsNullOrEmpty(Manifest) Then
                'Create an XPathDocument from the Xml
                Dim doc As New XPathDocument(New StringReader(Manifest))
                Dim nav As XPathNavigator = doc.CreateNavigator().SelectSingleNode("package")

                If InstallMode = InstallMode.Install Then
                    'Get Name Property
                    _Name = Util.ReadAttribute(nav, "name", Log, Util.EXCEPTION_NameMissing)

                    'Get Type
                    _PackageType = Util.ReadAttribute(nav, "type", Log, Util.EXCEPTION_TypeMissing)
                    CheckSecurity()
                    If Not IsValid Then
                        Exit Sub
                    End If

                    'Get Version
                    _Version = Util.ReadAttribute(nav, "version", Log, Util.EXCEPTION_VersionMissing)
                    ValidateVersion()
                    If Not IsValid Then
                        Exit Sub
                    End If

                    Log.AddInfo(Util.DNN_ReadingPackage + " - " + PackageType + " - " + Name)

                    'Get Friendly Name
                    _FriendlyName = Util.ReadElement(nav, "friendlyname", _Name)

                    'Get Description
                    _Description = Util.ReadElement(nav, "description")

                    'Get License
                    Dim licenseNav As XPathNavigator = nav.SelectSingleNode("vendor/license")
                    If licenseNav IsNot Nothing Then
                        Dim licenseSrc As String = Util.ReadAttribute(licenseNav, "src")
                        If String.IsNullOrEmpty(licenseSrc) Then
                            'Load from element
                            _License = licenseNav.Value
                        Else
                            'Load from file
                            Dim objStreamReader As StreamReader
                            objStreamReader = File.OpenText(InstallerInfo.TempInstallFolder + "\" + licenseSrc)
                            _License = objStreamReader.ReadToEnd
                            objStreamReader.Close()
                        End If
                    End If

                    'Parse the Dependencies
                    Dim dependency As IDependency = Nothing
                    For Each dependencyNav As XPathNavigator In nav.CreateNavigator().Select("dependencies/dependency")
                        dependency = DependencyFactory.GetDependency(dependencyNav)
                        If Not dependency.IsValid Then
                            _IsValid = False
                            InstallerInfo.Log.AddFailure(dependency.ErrorMessage)
                            Exit Sub
                        End If
                    Next
                End If

                'Parse the component nodes
                For Each componentNav As XPathNavigator In nav.CreateNavigator().Select("components/component")
                    'Set default order to next value (ie the same as the size of the collection)
                    Dim order As Integer = Components.Count

                    Dim type As String = componentNav.GetAttribute("type", "")

                    If InstallMode = InstallMode.Install Then
                        Dim installOrder As String = componentNav.GetAttribute("installOrder", "")
                        If Not String.IsNullOrEmpty(installOrder) Then
                            order = Integer.Parse(installOrder)
                        End If
                    Else
                        Dim unInstallOrder As String = componentNav.GetAttribute("unInstallOrder", "")
                        If Not String.IsNullOrEmpty(unInstallOrder) Then
                            order = Integer.Parse(unInstallOrder)
                        End If
                    End If

                    If InstallerInfo IsNot Nothing Then
                        Log.AddInfo(Util.DNN_ReadingComponent + " - " + type)
                    End If

                    Components.Add(order, New ComponentInfo(type, componentNav, Me))
                Next
            End If
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The Rollback method rolls back the package installation
        ''' </summary>
        ''' <history>
        ''' 	[cnurse]	07/31/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub Rollback()
            For index As Integer = 0 To Components.Count - 1
                Dim comp As ComponentInfo = Components.Values(index)
                If comp.Completed Then
                    comp.Rollback()
                End If
            Next
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The Uninstall method uninstalls the components of the package
        ''' </summary>
        ''' <history>
        ''' 	[cnurse]	07/25/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub UnInstall()
            'Iterate through all the Components
            For index As Integer = 0 To Components.Count - 1
                Dim comp As ComponentInfo = Components.Values(index)
                comp.UnInstall()

                If Log.Valid Then
                    Log.AddInfo(Util.UNINSTALL_SuccessComp + " - " + comp.Type)
                Else
                    Log.AddWarning(Util.UNINSTALL_Failure + " - " + comp.Type)
                End If
            Next

            If Log.Valid Then
                'Remove the Package information from the Data Store
                PackageController.DeletePackage(PackageID)
            End If
        End Sub

#End Region

    End Class

End Namespace
