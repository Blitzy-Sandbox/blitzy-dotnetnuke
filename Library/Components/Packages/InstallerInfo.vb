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
Imports System.IO
Imports System.Xml.XPath

Imports ICSharpCode.SharpZipLib.Zip

Namespace DotNetNuke.Services.Packages

    ''' -----------------------------------------------------------------------------
    ''' <summary>
    ''' The InstallerInfo class holds all the information associated with a
    ''' Installation.
    ''' </summary>
    ''' <remarks>
    ''' </remarks>
    ''' <history>
    ''' 	[cnurse]	07/24/2007  created
    ''' </history>
    ''' -----------------------------------------------------------------------------
    Public Class InstallerInfo

#Region "Private Members"

        Private _Files As New Dictionary(Of String, InstallFile)
        Private _InstallMode As InstallMode = InstallMode.Install
        Private _Log As New Logger
        Private _ManifestFile As InstallFile
        Private _Packages As New Dictionary(Of String, PackageInfo)
        Private _PhysicalSitePath As String = Null.NullString
        Private _PortalID As Integer = Null.NullInteger
        Private _SecurityAccessLevel As SecurityAccessLevel = SecurityAccessLevel.Host
        Private _TempInstallFolder As String = Null.NullString

#End Region

#Region "Constructors"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' This Constructor creates a new InstallerInfo instance
        ''' </summary>
        ''' <history>
        ''' 	[cnurse]	07/26/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub New()

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' This Constructor creates a new InstallerInfo instance from a Stream and a
        ''' string representing the physical path to the root of the site
        ''' </summary>
        ''' <param name="inputStream">The Stream to use to create this InstallerInfo instance</param>
        ''' <param name="sitePath">The physical path to the root of the site</param>
        ''' <history>
        ''' 	[cnurse]	07/24/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub New(ByVal inputStream As Stream, ByVal sitePath As String)
            _TempInstallFolder = DotNetNuke.Common.HostMapPath + "Temp\" + Path.GetFileNameWithoutExtension(Path.GetRandomFileName)
            _PhysicalSitePath = sitePath
            _InstallMode = InstallMode.Install

            'Read the Zip file into its component entries
            ReadZipStream(inputStream)
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' This Constructor creates a new InstallerInfo instance from a string representing
        ''' the physical path to the temporary install folder and a string representing 
        ''' the physical path to the root of the site
        ''' </summary>
        ''' <param name="tempFolder">The physical path to the zip file containg the package</param>
        ''' <param name="manifest">The manifest filename</param>
        ''' <param name="sitePath">The physical path to the root of the site</param>
        ''' <history>
        ''' 	[cnurse]	08/13/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub New(ByVal tempFolder As String, ByVal manifest As String, ByVal sitePath As String)
            _TempInstallFolder = tempFolder
            _PhysicalSitePath = sitePath
            _InstallMode = InstallMode.Install

            _ManifestFile = New InstallFile(manifest, Me)

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' This Constructor creates a new InstallerInfo instance from a PackageInfo object
        ''' </summary>
        ''' <param name="package">The PackageInfo instance</param>
        ''' <param name="sitePath">The physical path to the root of the site</param>
        ''' <history>
        ''' 	[cnurse]	07/31/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub New(ByVal package As PackageInfo, ByVal sitePath As String)
            Log.StartJob(Util.DNN_Reading)
            _PhysicalSitePath = sitePath
            _TempInstallFolder = DotNetNuke.Common.HostMapPath + "Temp\" + Path.GetFileNameWithoutExtension(Path.GetRandomFileName)
            _InstallMode = InstallMode.UnInstall
            package.AttachInstallerInfo(Me)
            Packages.Add(package.Name, package)
            package.ReadManifest()
            Log.EndJob(Util.DNN_Success)
        End Sub

#End Region

#Region "Public Properties"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets a Dictionary of Files that are included in the Package
        ''' </summary>
        ''' <value>A Dictionary(Of String, InstallFile)</value>
        ''' <history>
        ''' 	[cnurse]	07/24/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public ReadOnly Property Files() As Dictionary(Of String, InstallFile)
            Get
                Return _Files
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the InstallMode
        ''' </summary>
        ''' <value>A InstallMode value</value>
        ''' <history>
        ''' 	[cnurse]	07/31/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public ReadOnly Property InstallMode() As InstallMode
            Get
                Return _InstallMode
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets whether the InstallerInfo instance is Valid
        ''' </summary>
        ''' <value>A Boolean value</value>
        ''' <history>
        ''' 	[cnurse]	08/13/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public ReadOnly Property IsValid() As Boolean
            Get
                Return Log.Valid
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the associated Logger
        ''' </summary>
        ''' <value>A Logger</value>
        ''' <history>
        ''' 	[cnurse]	07/24/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public ReadOnly Property Log() As Logger
            Get
                Return _Log
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets and Sets the Manifest File for the Package
        ''' </summary>
        ''' <value>An InstallFile</value>
        ''' <history>
        ''' 	[cnurse]	07/24/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public ReadOnly Property ManifestFile() As InstallFile
            Get
                Return _ManifestFile
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets a Dictionary of Packages that are included in the Package Zip
        ''' </summary>
        ''' <value>A Dictionary(Of String, InstallPackage)</value>
        ''' <history>
        ''' 	[cnurse]	07/24/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public ReadOnly Property Packages() As Dictionary(Of String, PackageInfo)
            Get
                Return _Packages
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the Physical Path to the root of the Site (eg D:\Websites\DotNetNuke")
        ''' </summary>
        ''' <value>A String</value>
        ''' <history>
        ''' 	[cnurse]	07/24/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public ReadOnly Property PhysicalSitePath() As String
            Get
                Return _PhysicalSitePath
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the Id of the current portal (-1 if Host)
        ''' </summary>
        ''' <value>An Integer</value>
        ''' <history>
        ''' 	[cnurse]	08/22/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Property PortalID() As Integer
            Get
                Return _PortalID
            End Get
            Set(ByVal value As Integer)
                _PortalID = value
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets and sets the security Access Level of the user that is calling the INstaller
        ''' </summary>
        ''' <value>A SecurityAccessLevel enumeration</value>
        ''' <history>
        ''' 	[cnurse]	08/22/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Property SecurityAccessLevel() As SecurityAccessLevel
            Get
                Return _SecurityAccessLevel
            End Get
            Set(ByVal value As SecurityAccessLevel)
                _SecurityAccessLevel = value
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the Temporary Install Folder used to unzip the archive (and to place the 
        ''' backups of existing files) during InstallMode
        ''' </summary>
        ''' <value>A String</value>
        ''' <history>
        ''' 	[cnurse]	08/01/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public ReadOnly Property TempInstallFolder() As String
            Get
                Return _TempInstallFolder
            End Get
        End Property

#End Region

#Region "Private Methods"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The ReadZipStream reads a zip stream, and loads the Files Dictionary
        ''' </summary>
        ''' <history>
        ''' 	[cnurse]	07/24/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub ReadZipStream(ByVal inputStream As Stream)
            Log.StartJob(Util.FILES_Reading)

            Dim unzip As New ZipInputStream(inputStream)
            Dim entry As ZipEntry = unzip.GetNextEntry()

            While Not (entry Is Nothing)
                If Not entry.IsDirectory Then
                    ' Add file to list
                    Dim file As New InstallFile(unzip, entry, Me)

                    Files(file.FullName.ToLower) = file

                    If file.Type = InstallFileType.Manifest Then
                        If ManifestFile Is Nothing Then
                            _ManifestFile = file
                        Else
                            Log.AddFailure((Util.EXCEPTION_MultipleDnn + ManifestFile.Name + " and " + file.Name))
                        End If
                    End If
                    Log.AddInfo(String.Format(Util.FILE_ReadSuccess, file.FullName))
                End If
                entry = unzip.GetNextEntry
            End While

            If ManifestFile Is Nothing Then
                Log.AddFailure(Util.EXCEPTION_MissingDnn)
            End If

            If Log.Valid Then
                Log.EndJob(Util.FILES_ReadingEnd)
            Else
                Log.AddFailure(New Exception(Util.EXCEPTION_FileLoad))
                Log.EndJob(Util.FILES_ReadingEnd)
            End If

            'Close the Zip Input Stream as we have finished with it
            inputStream.Close()
        End Sub

#End Region

#Region "Public Methods"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The Install method installs the packages
        ''' </summary>
        ''' <history>
        ''' 	[cnurse]	07/25/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub Install()
            'Iterate through all the Packages
            For Each kvp As KeyValuePair(Of String, PackageInfo) In Packages
                'Check if package is valid
                If kvp.Value.IsValid Then
                    Log.AddInfo(Util.INSTALL_Start + " - " + kvp.Value.Name)
                    kvp.Value.Install()
                    If Log.Valid Then
                        Log.AddInfo(Util.INSTALL_Success + " - " + kvp.Value.Name)
                    Else
                        Log.AddInfo(Util.INSTALL_Failed + " - " + kvp.Value.Name)
                    End If
                Else
                    Log.AddFailure(Util.INSTALL_Aborted + " - " + kvp.Value.Name)
                End If
            Next

            'Delete Temp Folder
            Directory.Delete(TempInstallFolder, True)
            Log.AddInfo(Util.FOLDER_DeletedBackup)
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The ReadManifest method reads the manifest file and parses it into packages.
        ''' </summary>
        ''' <history>
        ''' 	[cnurse]	07/24/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub ReadManifest()
            Log.StartJob(Util.DNN_Reading)

            If ManifestFile IsNot Nothing Then
                'Create an XPathDocument from the Xml
                Dim doc As New XPathDocument(New FileStream(ManifestFile.TempFileName, FileMode.Open, FileAccess.Read))

                'Parse the package nodes
                For Each nav As XPathNavigator In doc.CreateNavigator().Select("dotnetnuke/packages")
                    Dim name As String = XmlUtils.GetNodeValue(nav, "package/name")
                    Packages.Add(name, New PackageInfo(nav.InnerXml, Me))
                Next
            End If

            If Log.Valid Then
                Log.EndJob(Util.DNN_Success)
            Else
                'Delete Temp Folder
                Directory.Delete(TempInstallFolder, True)
            End If
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The UnInstall method uninstalls the packages
        ''' </summary>
        ''' <history>
        ''' 	[cnurse]	07/25/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub UnInstall()
            'Iterate through all the Packages
            For Each kvp As KeyValuePair(Of String, PackageInfo) In Packages
                Log.AddInfo(Util.UNINSTALL_Start + " - " + kvp.Value.Name)
                kvp.Value.UnInstall()
                Log.AddInfo(Util.UNINSTALL_Success + " - " + kvp.Value.Name)
            Next
        End Sub

#End Region

    End Class

End Namespace

