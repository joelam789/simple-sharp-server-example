
import {Router, RouterConfiguration} from 'aurelia-router';
import {autoinject} from 'aurelia-framework';

import {Messenger} from './messenger';

@autoinject()
export class App {

	router: Router;

	private static _config: any = null;
	private static _containers: { [name: string]: any }  = { };

	constructor(public messenger: Messenger) {
		App._config = (<any>window).mainAppConfig;

		App._containers['app'] = document.getElementById('app');
		App._containers['loading'] = document.getElementById('loading');
	}

	static get config(): any {
		return App._config;
	}

	static get containers(): { [name: string]: any } {
		return App._containers;
	}

	configureRouter(config: RouterConfiguration, router: Router) {

		config.title = 'MiniBaccarat - H5 Client Demo';
		config.map([
			{ route: ['', 'login'],  moduleId: 'login',    name: 'login',    title: 'Login'},
			{ route: 'lobby', 		 moduleId: 'lobby',    name: 'lobby',    title: 'Lobby'},
			{ route: 'game-table',   moduleId: 'game-table',  name: 'game-table',  title: 'Game Table'},
		]);
		
		this.router = router;
	}
}
