
Describe 'Message publishing tests' {

    BeforeAll{
        . $PSScriptRoot\init.ps1
    }

    # BeforeEach{
    #     Write-Host 'here'
    # }

    Context 'firt try'{

        It 'It is simple basic test. Publish one message and than fetch from the storage.' {
            #ARRANGE
            $request = New-SendMessageRequest -TopicName "TestTopic" -MessageBody "test message body"

            #ACT
            $r = Publish-Message -SendMessageRequest $request
            Start-Sleep -Seconds 1
            $message = Get-Message -id $request.Header.MessageId

            #ASSERT
            $message | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Workers tests'{
        It 'sends messages on topic that should be picked by two workers'{
            #ACT
            $messagesBefore = Get-AllMessages | ForEach-Object { $_.attributes.messageId.ToString() }
            for($i=0; $i -lt 200; $i++) {
                $request = New-SendMessageRequest -TopicName "WorkerTestTopic1" -MessageBody "message picked by worker: "
                Publish-Message -SendMessageRequest $request
            }
            Start-Sleep -Seconds 3
            $messagesAfter = Get-AllMessages | ForEach-Object { $_.attributes.messageId.ToString() }
            $newMessages = $messagesAfter | select -Unique | where { $messagesBefore -notcontains $_ }

            $worker1Messages = $newMessages | Where-Object {$_.StartsWith('worker1.')}
            $worker2Messages = $newMessages | Where-Object {$_.StartsWith('worker2.')}

            #ASSERT
            $newMessages.Count | Should -BeExactly 200
            ($worker1Messages.Count + $worker2Messages.Count) | Should -BeExactly 200
            $worker1Messages.Count | Should -BeGreaterOrEqual ($worker2Messages.Count - 20)
            $worker1Messages.Count | Should -BeLessOrEqual ($worker2Messages.Count + 20)
        }
    }

    Context 'PubSub Tests'{
        It 'Send messages to topic that have bounded 2 subscribers and make sure that both subsribers processed all messages'{
            #ACT
            $messagesBefore = Get-AllMessages | ForEach-Object { $_.attributes.messageId.ToString() }

            for($i=0; $i -lt 100; $i++) {
                $request = New-SendMessageRequest -TopicName "PubSubTopic1" -MessageBody "message picked by subsriber: "
                Publish-Message -SendMessageRequest $request
            }

            Start-Sleep -Seconds 3
            $messagesAfter = Get-AllMessages | ForEach-Object { $_.attributes.messageId.ToString() }
            $newMessages = $messagesAfter | select -Unique | where { $messagesBefore -notcontains $_ }

            $subscriber1Messages = $newMessages | Where-Object {$_.StartsWith('subscriber1.')}
            $subscriber2Messages = $newMessages | Where-Object {$_.StartsWith('subscriber2.')}

            #ASSEERT
            $newMessages.Count | Should -BeExactly 200
            $subscriber1Messages.Count | Should -BeExactly 100
            $subscriber2Messages.Count | Should -BeExactly 100
        }
    }

    Context 'Retry testing'{
        It 'Checks that subscriber will aknowledge message on second attempt'{
             #ARRANGE
             $retrySettings = @{
                RetryCount = 1; #subscriber whill throw an error on first attempt.
             }
             $messageBody = ConvertTo-Json $retrySettings
             $request = New-SendMessageRequest -TopicName "TestRetryTopic" -MessageBody $messageBody -MessageBodyType "TestAppCommon.RetryInfo, TestAppCommon"

             #Act
             Publish-Message -SendMessageRequest $request
             Start-Sleep -Seconds 1
             $message = Get-Message -id $request.Header.MessageId
             #$payload = $message.payload | ConvertFrom-Json

             #Assert
             $message.payload.AttemptedCount | Should -BeExactly 2
        }
        
        It 'Show that messae will be aknowledged on 16th attempt, before that, 3 times will be requeued' {
            #ARRANGE
            $retrySettings = @{
                RetryCount = 15;
             }
             $messageBody = ConvertTo-Json $retrySettings
             $request = New-SendMessageRequest -TopicName "TestRetryTopic" -MessageBody $messageBody -MessageBodyType "TestAppCommon.RetryInfo, TestAppCommon"

             #Act
             Publish-Message -SendMessageRequest $request
             Start-Sleep -Seconds 4
             $dleMessages = Poll-Messages -queue 'TestRetryTopic-DLE-DefaultQueue' 
             $dleMessageBodies = $dleMessages | ForEach-Object { ConvertFrom-Json $_.Body }
             $dleMessage = $dleMessageBodies | Where-Object { $_.Attributes.MessageId -eq $request.Header.MessageId}
             $message = Get-Message -id $request.Header.MessageId

             #ASSERT
             $dleMessage | Should -BeNullOrEmpty
             $message | Should -Not -BeNullOrEmpty
             $message.payload.AttemptedCount | Should -BeExactly 16
        }
    }

    Context 'Throttling tests'{
        It 'tests expected amount parallel workes' -Skip {
            #ARRANGE
            $allRequests = @()
            for ($i = 0; $i -lt 400; $i++) {
                $delayInfo = @{
                    DelaySeconds = Get-Random -Minimum 3 -Maximum 9;
                }   
                $body = ConvertTo-Json $delayInfo
                $allRequests += New-SendMessageRequest -TopicName "ThrottlingWorkerTestTopic1" -MessageBody $body -MessageBodyType "TestAppCommon.DelayInfo, TestAppCommon"
            }
            
            #ACT
            Publish-Messages -SendMessageRequests $allRequests
            Start-Sleep -Seconds 3
            $activeWorksers = Get-ActiveWorksers

            #ASSERT
            $activeWorksers.Count | Should -BeExactly 20
        }
    }

    Context 'Dead letter testing' -Skip {
        BeforeAll{
            . ..\Python\venv\Scripts\activate
        }
        It 'Sends message that cannot be processed and check if message landed in dead letter exchange'{
            #ARRANGE
            $retrySettings = @{
                RetryCount = 1;
                AlwaysThrow = $true #indicate that subscriber should always throw an exception.
             }
             $messageBody = ConvertTo-Json $retrySettings
             $request = New-SendMessageRequest -TopicName "TestRetryTopic" -MessageBody $messageBody -MessageBodyType "TestAppCommon.RetryInfo, TestAppCommon"

             #ACT
             Publish-Message -SendMessageRequest $request
             Start-Sleep -Seconds 3
             $dleMessages = Poll-Messages -queue 'TestRetryTopic-DLE-DefaultQueue' 
             $dleMessageBodies = $dleMessages | ForEach-Object { ConvertFrom-Json $_.Body }
             $message = $dleMessageBodies | Where-Object { $_.Attributes.MessageId -eq $request.Header.MessageId}
            
             #ASSERT
             $message | Should -Not -BeNullOrEmpty
        }

        It 'Sends message with retry count which is "(requeue + 1) * (retry count + 1) +1" so message end up in dead letter queue.
            Then run script to requeue message from DL and make sure that is aknowledged'{
                #Helper function for polling message from DL queue
                function PollMessageFrom-Dle {
                    param (
                    [Parameter(Mandatory = $true)]
                    [string] $DleQueue,
                    [Parameter(Mandatory = $true)]
                    [string] $MessageId
                    )
                    $dleMessages = Poll-Messages -queue $DleQueue 
                    $dleMessageBodies = $dleMessages | ForEach-Object { ConvertFrom-Json $_.Body }
                    $dleMessage = $dleMessageBodies | Where-Object { $_.Attributes.MessageId -eq $MessageId }    
                    $dleMessage
                }
                #ARRANGE
                $retrySettings = @{
                    RetryCount = 5;
                }
                $messageBody = ConvertTo-Json $retrySettings
                $request = New-SendMessageRequest -TopicName "TestReturnFromDleTopic" -MessageBody $messageBody -MessageBodyType "TestAppCommon.RetryInfo, TestAppCommon"

                #ACT
                #Publsish message
                Publish-Message -SendMessageRequest $request
                #Wait that message lands to DL queue
                Start-Sleep -Seconds 4
                
                #Poll message from DLE
                $dleMessageBefore = PollMessageFrom-Dle -DleQueue 'TestReturnFromDleTopic-DLE-DefaultQueue' -MessageId $request.Header.MessageId
                #Poll message from repository. Should not exits
                $messageBeofre = Get-Message -id $request.Header.MessageId
                . py .\Python\requeue-from-dl.py 'TestReturnFromDleTopic-DLE-DefaultQueue' $request.Header.MessageId
                Start-Sleep -Seconds 2
                $dleMessageAfter = PollMessageFrom-Dle -DleQueue 'TestReturnFromDleTopic-DLE-DefaultQueue' -MessageId $request.Header.MessageId
                $messageAfter = Get-Message -id $request.Header.MessageId

                $messageBeofre | Should -BeNullOrEmpty
                $dleMessageBefore | Should -Not -BeNullOrEmpty
                $messageAfter | Should -Not -BeNullOrEmpty
                $dleMessageAfter | Should -BeNullOrEmpty
                $messageAfter.payload.AttemptedCount | Should -BeExactly 6
        }
    }
}