function Get-RandomString([int]$Size = 32) {
    $letters = Get-Variable -Scope script -Name $MyInvocation.MyCommand.Name -ErrorAction 0 | % Value
    if (!$letters) {
        $letters = ('a'..'z') + ('A'..'Z') + (0..9) + ('.','_')
        New-Variable -Scope script -Name $MyInvocation.MyCommand.Name -Value $letters
    }

    $r = 1..$Size | % { $letters[ (Get-Random $letters.Length ) ] }
    $r = -join $r
    $r = $r -replace '(^[_.0-9])|([_.]$)', 'A'  # replace starting/ending symbols and starting numbers
    $r = $r -replace '(?<=[_.])[_.]', 'A'       # replace multiple consecutive symbols
    $r
}