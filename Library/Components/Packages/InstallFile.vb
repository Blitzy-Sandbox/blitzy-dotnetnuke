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

Imports ICSharpCode.SharpZipLib.Zip
Imports System.IO

Namespace DotNetNuke.Services.Packages

    ''' -----------------------------------------------------------------------------
    ''' <summary>
    ''' The InstallFile class represents a single file in an Installer Package
    ''' </summary>
    ''' <remarks>
    ''' </remarks>
    ''' <history>
    ''' 	[cnurse]	07/24/2007  created
    ''' </history>
    ''' -----------------------------------------------------------------------------
    Public Class InstallFile

#Region "Private Members"

        Private _Encoding As TextEncoding
        Private _InstallerInfo As InstallerInfo
        Private _Name As String
        Private _Path As String
        Private _Type As InstallFileType
        Private _Version As String

#End Region

#Region "Constructors"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' This Constructor creates a new InstallFile instance from a ZipInputStream and a ZipEntry
        ''' </summary>
        ''' <remarks>The ZipInputStream is read into a byte array (Buffer), and the ZipEntry is used to
        ''' set up the properties of the InstallFile class.</remarks>
        ''' <param name="zip">The ZipInputStream</param>
        ''' <param name="entry">The ZipEntry</param>
        ''' <history>
        ''' 	[cnurse]	07/24/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub New(ByVal zip As ZipInputStream, ByVal entry As ZipEntry, ByVal info As InstallerInfo)
            _InstallerInfo = info
            ReadZip(zip, entry)
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' This Constructor creates a new InstallFile instance
        ''' </summary>
        ''' <param name="fileName">The fileName of the File</param>
        ''' <history>
        ''' 	[cnurse]	07/31/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub New(ByVal fileName As String)
            ParseFileName(fileName)
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' This Constructor creates a new InstallFile instance
        ''' </summary>
        ''' <param name="fileName">The fileName of the File</param>
        ''' <history>
        ''' 	[cnurse]	07/31/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub New(ByVal fileName As String, ByVal info As InstallerInfo)
            ParseFileName(fileName)
            _InstallerInfo = info
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' This Constructor creates a new InstallFile instance
        ''' </summary>
        ''' <param name="fileName">The file name of the File</param>
        ''' <param name="filePath">The file path of the file</param>
        ''' <history>
        ''' 	[cnurse]	08/03/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub New(ByVal fileName As String, ByVal filePath As String)
            _Name = fileName
            _Path = filePath
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' This Constructor creates a new InstallFile instance
        ''' </summary>
        ''' <param name="fileName">The file name of the File</param>
        ''' <param name="filePath">The file path of the file</param>
        ''' <history>
        ''' 	[cnurse]	08/03/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub New(ByVal fileName As String, ByVal filePath As String, ByVal info As InstallerInfo)
            Me.New(fileName, filePath)
            _InstallerInfo = info
        End Sub

#End Region

#Region "Public Properties"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the location of the backup file
        ''' </summary>
        ''' <value>A string</value>
        ''' <history>
        ''' 	[cnurse]	08/02/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public ReadOnly Property BackupFileName() As String
            Get
                Return System.IO.Path.Combine(BackupPath, Name + ".config")
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the location of the backup folder
        ''' </summary>
        ''' <value>A string</value>
        ''' <history>
        ''' 	[cnurse]	08/02/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public ReadOnly Property BackupPath() As String
            Get
                Return System.IO.Path.Combine(System.IO.Path.Combine(InstallerInfo.TempInstallFolder, "Backup"), Path)
            End Get
        End Property

        Public ReadOnly Property Encoding() As TextEncoding
            Get
                Return _Encoding
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the File Extension of the file
        ''' </summary>
        ''' <value>A string</value>
        ''' <history>
        ''' 	[cnurse]	07/24/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public ReadOnly Property Extension() As String
            Get
                Dim ext As String = System.IO.Path.GetExtension(_Name)
                If ext Is Nothing Or ext.Length = 0 Then
                    Return ""
                Else
                    Return ext.Substring(1)
                End If
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the Full Name of the file
        ''' </summary>
        ''' <value>A string</value>
        ''' <history>
        ''' 	[cnurse]	07/24/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public ReadOnly Property FullName() As String
            Get
                Return System.IO.Path.Combine(_Path, _Name)
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the associated InstallerInfo
        ''' </summary>
        ''' <value>An InstallerInfo object</value>
        ''' <history>
        ''' 	[cnurse]	08/02/2007  created
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
        ''' Gets the Name of the file
        ''' </summary>
        ''' <value>A string</value>
        ''' <history>
        ''' 	[cnurse]	07/24/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public ReadOnly Property Name() As String
            Get
                Return _Name
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the Path of the file
        ''' </summary>
        ''' <value>A string</value>
        ''' <history>
        ''' 	[cnurse]	07/24/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public ReadOnly Property Path() As String
            Get
                Return _Path
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the location of the temporary file
        ''' </summary>
        ''' <value>A string</value>
        ''' <history>
        ''' 	[cnurse]	08/02/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public ReadOnly Property TempFileName() As String
            Get
                Return System.IO.Path.Combine(InstallerInfo.TempInstallFolder, FullName)
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the Type of the file
        ''' </summary>
        ''' <value>An InstallFileType Enumeration</value>
        ''' <history>
        ''' 	[cnurse]	07/24/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public ReadOnly Property Type() As InstallFileType
            Get
                Return _Type
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the Version of the file
        ''' </summary>
        ''' <value>A String</value>
        ''' <history>
        ''' 	[cnurse]	08/01/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public ReadOnly Property Version() As String
            Get
                Return _Version
            End Get
        End Property

#End Region

#Region "Private Methods"

        Private Function GetTextEncodingType(ByVal Buffer As Byte()) As TextEncoding
            'UTF7 = No byte higher than 127
            'UTF8 = first three bytes EF BB BF
            'UTF16BigEndian = first two bytes FE FF
            'UTF16LittleEndian = first two bytes FF FE

            'Lets do the easy ones first
            If Buffer(0) = 255 And Buffer(1) = 254 Then
                Return TextEncoding.UTF16LittleEndian
            End If
            If Buffer(0) = 254 And Buffer(1) = 255 Then
                Return TextEncoding.UTF16BigEndian
            End If
            If Buffer(0) = 239 And Buffer(1) = 187 And Buffer(2) = 191 Then
                Return TextEncoding.UTF8
            End If

            'This does a simple test to verify that there are no bytes with a value larger than 127
            'which would be invalid in UTF-7 encoding
            Dim i As Integer
            For i = 0 To 100
                If Buffer(i) > 127 Then
                    Return TextEncoding.Unknown
                End If
            Next
            Return TextEncoding.UTF7

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The ParseFileName parses the ZipEntry metadata
        ''' </summary>
        ''' <param name="fileName">A String representing the file name</param>
        ''' <history>
        ''' 	[cnurse]	07/24/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub ParseFileName(ByVal fileName As String)
            Dim i As Integer = fileName.Replace("\", "/").LastIndexOf("/")
            If i < 0 Then
                _Name = fileName.Substring(0, fileName.Length)
                _Path = ""
            Else
                _Name = fileName.Substring(i + 1, fileName.Length - (i + 1))
                _Path = fileName.Substring(0, i)
            End If

            Select Case Extension.ToLower
                Case "ascx"
                    _Type = InstallFileType.Ascx
                Case "dll"
                    _Type = InstallFileType.Assembly
                Case "dnn"
                    _Type = InstallFileType.Manifest
                Case "resources"
                    _Type = InstallFileType.Resources
                Case "sql"
                    _Type = InstallFileType.Script
                Case Else
                    If Extension.ToLower.EndsWith("dataprovider") Then
                        _Type = InstallFileType.Script
                    Else
                        _Type = InstallFileType.Other
                    End If
            End Select

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The ReadZip method reads the zip stream and parses the ZipEntry metadata
        ''' </summary>
        ''' <param name="unzip">A ZipStream containing the file content</param>
        ''' <param name="entry">A ZipEntry containing the file metadata</param>
        ''' <history>
        ''' 	[cnurse]	07/24/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub ReadZip(ByVal unzip As ZipInputStream, ByVal entry As ZipEntry)
            ParseFileName(entry.Name)

            Util.WriteStream(unzip, TempFileName)
        End Sub

#End Region

#Region "Public Methods"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The SetVersion method sets the version of the file
        ''' </summary>
        ''' <param name="version">The version of the file</param>
        ''' <history>
        ''' 	[cnurse]	07/24/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub SetVersion(ByVal version As String)
            _Version = version
        End Sub

#End Region

    End Class
End Namespace
