from collections import namedtuple
import json
import sys
import pika

print("System parameters:")
for p in sys.argv:
    print(p)


# Set Dead Letter Exchange name that you want to poll messages
DLE_QUEUE = None #'TestRetryTopic-DLE-DefaultQueue'
# Set Exchange name that messages will be pushed
MESSAGE_ID = None  # 'f0c72066-25e3-4f23-ab26-ef16cefa856c'

HOST_NAME = 'localhost'
CRED_USR = 'guest'
CRED_PWD = 'guest'


Message = namedtuple('Message', 'method_frame header_frame, body')


def get_messages(channel, queue_name: str, message_id=None) -> [Message]:
    have_queued_messages = True
    messages = []
    while have_queued_messages:
        method_frame, header_frame, body = channel.basic_get(queue_name)
        if method_frame:
            if message_id:
                message_body = json.loads(body)
                if message_body.get('Attributes', {}).get('MessageId', {}) == message_id:
                    messages.append(Message(method_frame, header_frame, body,))
            else:
                messages.append(Message(method_frame, header_frame, body,))
        else:
            have_queued_messages = False
            print('No more messages')

    return messages


def publish_messages(channel, messages: [Message]):
    for message in messages:
        topic = message.header_frame.headers['x-first-death-exchange']
        subscription = message.header_frame.headers['x-first-death-queue']
        channel.basic_publish(exchange=topic,
                              routing_key=subscription,
                              body=message.body)
        channel.basic_ack(message.method_frame.delivery_tag)
        print(f"Published message to topic {topic} and directed for subscription {subscription}")


def main():
    global DLE_QUEUE
    global HOST_NAME
    global CRED_USR
    global CRED_PWD
    global MESSAGE_ID
    dle_queue = sys.argv[1] if len(sys.argv) > 1 else DLE_QUEUE
    message_id = sys.argv[2] if len(sys.argv) > 2 else MESSAGE_ID
    print(f'DLE queue: {dle_queue}')
    print(f'Message ID: {message_id}')
    connection_parameters = pika.ConnectionParameters(host=HOST_NAME,
                                                      credentials=pika.credentials.PlainCredentials(username=CRED_USR,
                                                                                                    password=CRED_PWD))
    connection = pika.BlockingConnection(connection_parameters)
    channel = connection.channel()
    print(f'Connected to {HOST_NAME}')
    messages = get_messages(channel, dle_queue, message_id)
    print(f"{len(messages)} will be requeued")
    publish_messages(channel, messages)


if __name__ == '__main__':
    main()
