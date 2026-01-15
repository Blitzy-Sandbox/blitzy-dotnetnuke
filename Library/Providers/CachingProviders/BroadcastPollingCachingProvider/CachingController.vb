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
Imports System.Collections
Imports System.Data
Imports System.Web.Caching
Imports System.Reflection
Imports System.Xml.Serialization
Imports System.IO
Imports DotNetNuke.Common.Utilities
Imports DotNetNuke.Services.Log.EventLog
Imports DotNetNuke.Services.Cache
Imports DotNetNuke.Services.Cache.BroadcastPollingCachingProvider.Data

Namespace DotNetNuke.Services.Cache.BroadcastPollingCachingProvider

    Public Class Controller

        Public Function GetCachedObject(ByVal Key As String, ByVal objType As Type) As Object
            Dim dr As IDataReader = DataProvider.Instance.GetCachedObject(Key)
            Try
                Dim obj As Object = Nothing
                If Not dr Is Nothing Then
                    While dr.Read()
                        Dim str As String = Convert.ToString(dr("CachedObjectValue"))
                        obj = Deserialize(str, objType)
                    End While
                End If
                Return obj
            Finally
                If Not dr Is Nothing Then
                    dr.Close()
                End If
            End Try
        End Function

        Public Sub AddCachedObject(ByVal Key As String, ByVal obj As Object, ByVal ServerName As String)
            If Not obj Is Nothing Then
                Dim str As String = Serialize(obj)
                DataProvider.Instance.AddCachedObject(Key, str, ServerName)
            End If
        End Sub

        Public Sub DeleteCachedObject(ByVal Key As String)

            DataProvider.Instance.DeleteCachedObject(Key)

        End Sub

        Public Function AddBroadcast(ByVal BroadcastType As String, ByVal BroadcastMessage As String, ByVal ServerName As String) As Integer

            Return DataProvider.Instance.AddBroadcast(BroadcastType, BroadcastMessage, ServerName)

        End Function

        Public Function GetBroadcasts(ByVal ServerName As String) As ArrayList

            Return CBO.FillCollection(DataProvider.Instance.GetBroadcasts(ServerName), GetType(BroadcastInfo))

        End Function

        Private Function Serialize(ByVal obj As Object) As String
            Dim str As String = XmlUtils.Serialize(obj)
            Return str
        End Function

        Private Function Deserialize(ByVal str As String, ByVal objType As Type) As Object
            Dim objStream As MemoryStream
            objStream = New MemoryStream(System.Text.ASCIIEncoding.Default.GetBytes(str))
            Dim serializer As XmlSerializer = New XmlSerializer(objType)
            Dim tr As TextReader = New StreamReader(objStream)
            Dim obj As Object = serializer.Deserialize(tr)
            Return obj
        End Function

    End Class

End Namespace
