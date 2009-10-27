﻿Imports System
Imports System.IO
Imports Ionic.Zip

Public Class Form1

    Private Delegate Sub ZipProgress(ByVal e As ZipProgressEventArgs)
    Dim _operationCanceled As Boolean
    Dim nFilesCompleted As Integer
    Dim totalEntriesToProcess As Integer
    Dim _appCuKey As Microsoft.Win32.RegistryKey
    Dim _extractThread As System.Threading.Thread
    Dim AppRegyPath As String = "Software\Ionic\VBunZip"
    Dim rvn_ZipFile As String = "zipfile"
    Dim rvn_ExtractDir As String = "extractdir"

    Private Sub btnZipBrowse_Click(ByVal sender As Object, ByVal e As EventArgs) Handles btnZipBrowse.Click
        Dim openFileDialog1 As New OpenFileDialog
        If (String.IsNullOrEmpty(tbZipToOpen.Text)) Then
            openFileDialog1.InitialDirectory = "c:\"
        Else
            openFileDialog1.InitialDirectory = IIf(File.Exists(Me.tbZipToOpen.Text), Path.GetDirectoryName(Me.tbZipToOpen.Text), Me.tbZipToOpen.Text)
        End If
        openFileDialog1.Filter = "zip files|*.zip|EXE files|*.exe|All Files|*.*"
        openFileDialog1.FilterIndex = 1
        openFileDialog1.RestoreDirectory = True
        If (openFileDialog1.ShowDialog = DialogResult.OK) Then
            Me.tbZipToOpen.Text = openFileDialog1.FileName
            If File.Exists(Me.tbZipToOpen.Text) Then
                Me.btnUnzip_Click(sender, e)
            End If
        End If
    End Sub


    Private Sub btnUnzip_Click(ByVal sender As Object, ByVal e As EventArgs) Handles btnUnzip.Click
        If Not String.IsNullOrEmpty(tbZipToOpen.Text) And _
        File.Exists(Me.tbZipToOpen.Text) And _
        Not String.IsNullOrEmpty(tbExtractDir.Text) And _
        Directory.Exists(tbExtractDir.Text) Then
            btnCancel.Enabled = True
            btnUnzip.Enabled = False
            KickoffExtract()
        End If
    End Sub


    Private Sub KickoffExtract()
        If Not String.IsNullOrEmpty(tbExtractDir.Text) Then
            lblStatus.Text = "Extracting..."
            Dim args(2) As Object
            args(0) = tbZipToOpen.Text
            args(1) = tbExtractDir.Text
            _extractThread = New System.Threading.Thread(New System.Threading.ParameterizedThreadStart(AddressOf UnzipFile))
            _extractThread.Start(args)
            Me.Cursor = Cursors.WaitCursor
        End If
    End Sub



    Private Sub btnExtractDirBrowse_Click(ByVal sender As Object, ByVal e As EventArgs) Handles btnExtractDirBrowse.Click
        Dim dlg As New FolderBrowserDialog
        dlg.Description = "Select a folder to zip up:"
        dlg.ShowNewFolderButton = False
        'dlg.ShowEditBox = True
        dlg.SelectedPath = Me.tbExtractDir.Text
        'dlg.ShowFullPathInEditBox = True
        If (dlg.ShowDialog = DialogResult.OK) Then
            tbExtractDir.Text = dlg.SelectedPath
        End If
    End Sub


    Private Sub UnzipFile(ByVal args As Object())
        Dim extractCancelled As Boolean = False
        Dim zipToRead As String = args(0)
        Dim extractDir As String = args(1)
        Try
            Using zip As ZipFile = ZipFile.Read(zipToRead)
                totalEntriesToProcess = zip.Entries.Count
                SetProgressBarMax(zip.Entries.Count)
                AddHandler zip.ExtractProgress, New EventHandler(Of ExtractProgressEventArgs)(AddressOf Me.zip_ExtractProgress)
                zip.ExtractAll(extractDir, Ionic.Zip.ExtractExistingFileAction.OverwriteSilently)
            End Using
        Catch ex1 As Exception
            MessageBox.Show(String.Format("There's been a problem extracting that zip file.  {0}", ex1.Message), "Error Extracting", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1)
        End Try
        ResetUI()
    End Sub

    Private Sub ResetUI()
        If btnCancel.InvokeRequired Then
            btnCancel.Invoke(New Action(AddressOf ResetUI), New Object() {})
        Else
            btnUnzip.Enabled = True
            btnCancel.Enabled = False
            ProgressBar1.Maximum = 1
            ProgressBar1.Value = 0
            Me.Cursor = Cursors.Arrow
        End If
    End Sub

    Private Sub SetProgressBarMax(ByVal n As Integer)
        If ProgressBar1.InvokeRequired Then
            ProgressBar1.Invoke(New Action(Of Integer)(AddressOf SetProgressBarMax), New Object() {n})
        Else
            ProgressBar1.Maximum = n
            ProgressBar1.Step = 1
        End If
    End Sub

    Private Sub zip_ExtractProgress(ByVal sender As Object, ByVal e As ExtractProgressEventArgs)
        If (e.EventType = Ionic.Zip.ZipProgressEventType.Extracting_AfterExtractEntry) Then
            StepEntryProgress(e)
        ElseIf (e.EventType = ZipProgressEventType.Extracting_BeforeExtractAll) Then
            'StepArchiveProgress(e)
        End If
    End Sub


    Private Sub StepEntryProgress(ByVal e As ZipProgressEventArgs)
        If ProgressBar1.InvokeRequired Then
            ProgressBar1.Invoke(New ZipProgress(AddressOf StepEntryProgress), New Object() {e})
        ElseIf Not _operationCanceled Then
            ProgressBar1.PerformStep()
            System.Threading.Thread.Sleep(100)
            'set a label with status information
            nFilesCompleted = nFilesCompleted + 1
            lblStatus.Text = String.Format("{0} of {1} files...({2})", nFilesCompleted, totalEntriesToProcess, e.CurrentEntry.FileName)
            Me.Update()
        End If
    End Sub



    'Private Sub StepArchiveProgress(ByVal e As ZipProgressEventArgs)
    '    If ProgressBar1.InvokeRequired Then
    '        ProgressBar1.Invoke(New ZipProgress(AddressOf StepArchiveProgress), New Object() {e})
    '    ElseIf Not _operationCanceled Then
    '        _nFilesCompleted = _nFilesCompleted + 1
    '        ProgressBar1.PerformStep()
    '        progressBar2.Value = progressBar2.Maximum = 1
    '        MyBase.Update()
    '    End If
    'End Sub

    Private Sub btnCancel_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnCancel.Click
        _operationCanceled = True
        ProgressBar1.Maximum = 1
        ProgressBar1.Value = 0
    End Sub


    Private Sub SaveFormToRegistry()
        If AppCuKey IsNot Nothing Then
            If Not String.IsNullOrEmpty(tbZipToOpen.Text) Then
                AppCuKey.SetValue(rvn_ZipFile, Me.tbZipToOpen.Text)
            End If
            If Not String.IsNullOrEmpty(tbExtractDir.Text) Then
                AppCuKey.SetValue(rvn_ExtractDir, tbExtractDir.Text)
            End If
        End If
    End Sub

    Private Sub LoadFormFromRegistry()
        If AppCuKey IsNot Nothing Then
            Dim s As String
            s = AppCuKey.GetValue(rvn_ZipFile)
            If Not String.IsNullOrEmpty(s) Then
                Me.tbZipToOpen.Text = s
            End If
            s = AppCuKey.GetValue(rvn_ExtractDir)
            If Not String.IsNullOrEmpty(s) Then
                tbExtractDir.Text = s
            End If
        End If
    End Sub


    Public ReadOnly Property AppCuKey() As Microsoft.Win32.RegistryKey
        Get
            If (_appCuKey Is Nothing) Then
                Me._appCuKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(AppRegyPath, True)
                If (Me._appCuKey Is Nothing) Then
                    Me._appCuKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(AppRegyPath)
                End If
            End If
            Return _appCuKey
        End Get
    End Property

    Private Sub Form1_FormClosing(ByVal sender As System.Object, ByVal e As System.Windows.Forms.FormClosingEventArgs) Handles MyBase.FormClosing
        SaveFormToRegistry()
    End Sub

    Private Sub Form1_Load(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles MyBase.Load
        LoadFormFromRegistry()
    End Sub
End Class

