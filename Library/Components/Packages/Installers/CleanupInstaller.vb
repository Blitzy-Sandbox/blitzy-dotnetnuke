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
Imports System.Data.Common
Imports System.IO
Imports System.Text
Imports System.Xml.XPath

Imports DotNetNuke.UI.Skins

Namespace DotNetNuke.Services.Packages

    ''' -----------------------------------------------------------------------------
    ''' <summary>
    ''' The CleanupInstaller cleans up (removes) files from previous versions
    ''' </summary>
    ''' <remarks>
    ''' </remarks>
    ''' <history>
    ''' 	[cnurse]	09/05/2007  created
    ''' </history>
    ''' -----------------------------------------------------------------------------
    Public Class CleanupInstaller
        Inherits FileInstaller

#Region "Protected Properties"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the PhysicalBasePath for the skin files
        ''' </summary>
        ''' <value>A String</value>
        ''' <history>
        ''' 	[cnurse]	07/25/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Protected Overrides ReadOnly Property PhysicalBasePath() As String
            Get
                Dim _PhysicalBasePath As String = PhysicalSitePath
                If Not _PhysicalBasePath.EndsWith("\") Then
                    _PhysicalBasePath += "\"
                End If
                Return _PhysicalBasePath
            End Get
        End Property

#End Region

#Region "Protected Methods"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The CleanupFile method cleansup a single file.
        ''' </summary>
        ''' <param name="insFile">The InstallFile to clean up</param>
        ''' <history>
        ''' 	[cnurse]	09/05/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Protected Function CleanupFile(ByVal insFile As InstallFile) As Boolean
            Try
                'Backup File
                If File.Exists(PhysicalBasePath + insFile.FullName) Then
                    Util.BackupFile(insFile, PhysicalBasePath, Log)
                End If

                'Delete file
                Util.DeleteFile(insFile, PhysicalBasePath, Log)
                Return True
            Catch ex As Exception
                Return False
            End Try
        End Function

        Protected Overrides Function ReadManifestItem(ByVal nav As System.Xml.XPath.XPathNavigator, ByVal checkFileExists As Boolean) As InstallFile
            Return MyBase.ReadManifestItem(nav, False)
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The RollbackFile method rolls back the cleanup of a single file.
        ''' </summary>
        ''' <param name="installFile">The InstallFile to commit</param>
        ''' <history>
        ''' 	[cnurse]	09/05/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Protected Overrides Sub RollbackFile(ByVal installFile As InstallFile)
            'Check for Backups
            If File.Exists(installFile.BackupFileName) Then
                Util.RestoreFile(installFile, PhysicalBasePath, Log)
            End If
        End Sub

#End Region

#Region "Public Methods"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The Commit method finalises the Install and commits any pending changes.
        ''' </summary>
        ''' <remarks>In the case of Clenup this is not neccessary</remarks>
        ''' <history>
        ''' 	[cnurse]	09/05/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Overrides Sub Commit()
            'Do nothing
            MyBase.Commit()
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The Install method cleansup the files
        ''' </summary>
        ''' <history>
        ''' 	[cnurse]	09/05/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Overrides Sub Install()
            Try
                Dim bSuccess As Boolean = True
                For Each file As InstallFile In Files
                    bSuccess = CleanupFile(file)
                    If Not bSuccess Then
                        Exit For
                    End If
                Next
                Completed = bSuccess
            Catch ex As Exception
                Log.AddFailure(Util.EXCEPTION + " - " + ex.Message)
            End Try
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The UnInstall method uninstalls the file component
        ''' </summary>
        ''' <remarks>There is no uninstall for this component</remarks>
        ''' <history>
        ''' 	[cnurse]	09/05/2007  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Overrides Sub UnInstall()
        End Sub

#End Region

    End Class

End Namespace
