mergeInto(LibraryManager.library, {
  $MequiFirebase: {
    ready: null,
    client: null,
    listeners: {},
    initialize: function () {
      if (!MequiFirebase.ready) {
        MequiFirebase.ready = Promise.all([
          import('https://www.gstatic.com/firebasejs/12.11.0/firebase-app.js'),
          import('https://www.gstatic.com/firebasejs/12.11.0/firebase-auth.js'),
          import('https://www.gstatic.com/firebasejs/12.11.0/firebase-firestore.js')
        ]).then(async function (modules) {
          var app = modules[0].initializeApp({"apiKey":"AIzaSyDrc9yDsCFrDIVS8dTYwmzAsfPbbOOcGsY","projectId":"mequi-break","authDomain":"mequi-break.firebaseapp.com"});
          var auth = modules[1].getAuth(app);
          var credential = await modules[1].signInAnonymously(auth);
          MequiFirebase.client = { sdk: modules[2], db: modules[2].getFirestore(app), uid: credential.user.uid };
          return MequiFirebase.client;
        }).catch(function (error) { MequiFirebase.ready = null; throw error; });
      }
      return MequiFirebase.ready;
    },
    upload: async function (client, document) {
      var sdk = client.sdk, db = client.db;
      document.ownerUid = client.uid;
      var session = sdk.doc(db, 'sessions/' + document.sessionId);
      var base = 'stores/' + document.storeKey + '/months/' + document.monthKey + '/shifts/';
      var all = sdk.doc(db, base + 'all/games/' + document.activity);
      var shift = sdk.doc(db, base + document.shiftKey + '/games/' + document.activity);
      await sdk.runTransaction(db, async function (transaction) {
        if ((await transaction.get(session)).exists()) return;
        var previousAll = await transaction.get(all);
        var previousShift = await transaction.get(shift);
        function add(previous) {
          var old = previous.exists() ? previous.data() : {};
          var updated = { lastSessionId: document.sessionId };
          Object.keys(document.metrics).forEach(function (key) {
            updated[key] = (old[key] || 0) + document.metrics[key];
            if (!Number.isSafeInteger(updated[key])) throw new Error('Metric overflow');
          });
          return updated;
        }
        transaction.set(session, document);
        transaction.set(all, add(previousAll));
        transaction.set(shift, add(previousShift));
      });
    }
  },
  MequiFirebaseRequest__deps: ['$MequiFirebase', '$UTF8ToString'],
  MequiFirebaseRequest: function (receiverPtr, requestPtr) {
    var receiver = UTF8ToString(receiverPtr), request = JSON.parse(UTF8ToString(requestPtr));
    var respond = function (result) { result.id = request.id; SendMessage(receiver, 'OnFirebaseWebResponse', JSON.stringify(result)); };
    if (request.operation === 'stop') {
      var stop = MequiFirebase.listeners[request.id];
      if (stop) stop();
      delete MequiFirebase.listeners[request.id];
      return;
    }
    if (request.operation === 'listen') MequiFirebase.listeners[request.id] = function () {};
    var timer;
    var work = MequiFirebase.initialize().then(async function (client) {
      if (request.operation === 'initialize') return { value: client.uid };
      if (request.operation === 'upload') { await MequiFirebase.upload(client, JSON.parse(request.payload)); return {}; }
      if (request.operation === 'listen' && MequiFirebase.listeners[request.id]) {
        var keys = ['sessions','completed','delivered','missed','impatientOrders','impatientMissed','reorders','assignments','breaks','breakReorganizations','stationProblems','stationRepairs','specialistPlacements','specialistOpportunities','choice0','choice1','choice2','choice3'];
        MequiFirebase.listeners[request.id] = client.sdk.onSnapshot(client.sdk.doc(client.db, request.payload), function (snapshot) {
          var fields = snapshot.exists() ? snapshot.data() : {};
          respond({ metrics: keys.map(function (key) { return Math.min(2147483647, Math.max(0, Math.trunc(Number(fields[key]) || 0))); }) });
        }, function (error) { respond({ error: error.code || error.message }); });
      }
      return null;
    });
    if (request.operation !== 'listen') work = Promise.race([work, new Promise(function (_, reject) {
      timer = setTimeout(function () { reject(new Error('Firebase request timed out')); }, 45000);
    })]);
    work.then(function (result) { if (result) respond(result); }).catch(function (error) {
      respond({ error: error.code || error.message || String(error) });
    }).finally(function () { clearTimeout(timer); });
  }
});
