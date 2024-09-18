<script>
    import { defineProps, reactive, onMounted } from 'vue';
    import * as signalR from '@microsoft/signalr';
    import axios from 'axios';
    import { errorMessages } from 'vue/compiler-sfc';
    let connection;
    export default {

        setup() {
            const state = reactive({
                importedCount: 0,
                totalCount: 0,
                errorMessages: "",
                importState: ""
            });

            const importExecute = async () => {
                await connection.invoke('ImportExecute');

            }

            onMounted(async () => {
                connection = new signalR.HubConnectionBuilder()
                    .withUrl('https://localhost:7011/hub/import')
                    .withAutomaticReconnect()
                    .build();
                await connection.start();
                connection.on('ImportProgress', (total, current) => {
                    state.importedCount = current;
                    state.totalCount = total;
                })
                connection.on('ImportError', msg => {
                    state.errorMessages = msg
                })
                connection.on("ImportState", msg => {
                    alert(msg);
                })
            })
            return { state, importExecute }
        }
    }

</script>

<template>
    <div>
        <input type="button" value="导入" @click="importExecute" />
        <progress :value="state.importedCount" :max="state.totalCount" />
        <div>
            {{state.errorMessage}}
            <div>{{ state.importedCount }}</div>
            <div>{{ state.totalCount }}</div>
        </div>
    </div>
</template>

