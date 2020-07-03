-- --------------------------------------------------------
-- Host:                         127.0.0.1
-- Server version:               8.0.18 - MySQL Community Server - GPL
-- Server OS:                    Win64
-- HeidiSQL Version:             11.0.0.5919
-- --------------------------------------------------------

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET NAMES utf8 */;
/*!50503 SET NAMES utf8mb4 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;


-- Dumping database structure for p2p_common_m
CREATE DATABASE IF NOT EXISTS `p2p_common_m` /*!40100 DEFAULT CHARACTER SET utf8 */ /*!80016 DEFAULT ENCRYPTION='N' */;
USE `p2p_common_m`;

-- Dumping structure for table p2p_common_m.tbl_bet_record
CREATE TABLE IF NOT EXISTS `tbl_bet_record` (
  `bet_uuid` varchar(64) NOT NULL,
  `server_code` varchar(45) NOT NULL,
  `table_code` varchar(45) NOT NULL,
  `shoe_code` varchar(45) NOT NULL,
  `round_number` int(11) NOT NULL DEFAULT '0',
  `client_id` varchar(45) NOT NULL DEFAULT '',
  `front_end` varchar(45) NOT NULL DEFAULT '',
  `session_id` varchar(64) NOT NULL DEFAULT '',
  `game_type` int(11) NOT NULL DEFAULT '0',
  `bet_type` int(11) NOT NULL DEFAULT '0',
  `bet_pool` int(11) NOT NULL DEFAULT '0',
  `coins_per_credit` decimal(19,2) DEFAULT '0.00',
  `bets_per_line` decimal(19,2) DEFAULT '0.00',
  `betted_lines` int(11) DEFAULT '0',
  `bet_amount` decimal(19,4) NOT NULL DEFAULT '0.0000',
  `game_input` varchar(500) NOT NULL DEFAULT '',
  `game_output` varchar(500) NOT NULL DEFAULT '',
  `game_result` varchar(500) NOT NULL DEFAULT '',
  `pay_amount` decimal(19,4) NOT NULL DEFAULT '0.0000',
  `contribution` decimal(19,4) NOT NULL DEFAULT '0.0000',
  `bet_state` int(11) NOT NULL DEFAULT '0',
  `settle_state` int(11) NOT NULL DEFAULT '0',
  `debit_state` int(11) NOT NULL DEFAULT '0',
  `credit_state` int(11) NOT NULL DEFAULT '0',
  `cancel_state` int(11) NOT NULL DEFAULT '0',
  `need_to_cancel` int(11) NOT NULL DEFAULT '0',
  `merchant_code` varchar(64) NOT NULL,
  `currency_code` varchar(5) NOT NULL DEFAULT 'CNY',
  `player_id` varchar(64) NOT NULL,
  `bet_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `settle_time` datetime DEFAULT NULL,
  `cancel_time` datetime DEFAULT NULL,
  `update_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `remark` varchar(1000) DEFAULT NULL,
  PRIMARY KEY (`bet_uuid`),
  KEY `IDX_SERVER_CODE` (`server_code`),
  KEY `IDX_ROUND_NUMBER` (`round_number`),
  KEY `IDX_CLIENT_ID` (`client_id`) /*!80000 INVISIBLE */,
  KEY `IDX_FRONT_END` (`front_end`),
  KEY `IDX_BET_POOL` (`bet_pool`),
  KEY `IDX_GAME_RESULT` (`game_result`),
  KEY `IDX_BET_STATE` (`bet_state`),
  KEY `IDX_PLAYER_ID` (`player_id`),
  KEY `IDX_BET_TIME` (`bet_time`),
  KEY `IDX_UPDATE_TIME` (`update_time`),
  KEY `IDX_TABLE_CODE` (`table_code`),
  KEY `IDX_SHOE_CODE` (`shoe_code`),
  KEY `IDX_GAME_ROUND` (`table_code`,`shoe_code`,`round_number`),
  KEY `IDX_BET_TYPE` (`bet_type`) /*!80000 INVISIBLE */,
  KEY `IDX_GAME_TYPE` (`game_type`),
  KEY `IDX_MERCHANT_CODE` (`merchant_code`),
  KEY `IDX_CURRENCY_CODE` (`currency_code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

-- Data exporting was unselected.

-- Dumping structure for table p2p_common_m.tbl_trans_credit
CREATE TABLE IF NOT EXISTS `tbl_trans_credit` (
  `credit_uuid` varchar(64) NOT NULL,
  `bet_uuid` varchar(64) NOT NULL,
  `game_code` varchar(45) NOT NULL,
  `round_id` varchar(45) NOT NULL,
  `bet_pool` int(11) NOT NULL,
  `provider_code` varchar(64) NOT NULL,
  `merchant_code` varchar(64) NOT NULL,
  `currency_code` varchar(5) NOT NULL DEFAULT 'CNY',
  `player_id` varchar(64) NOT NULL,
  `client_id` varchar(45) NOT NULL DEFAULT '',
  `session_id` varchar(64) NOT NULL DEFAULT '',
  `credit_amount` decimal(19,4) NOT NULL DEFAULT '0.0000',
  `request_times` int(11) NOT NULL DEFAULT '0',
  `process_state` int(11) NOT NULL DEFAULT '0',
  `is_success` int(11) NOT NULL DEFAULT '0',
  `is_cancelled` int(11) NOT NULL DEFAULT '0',
  `network_error` int(11) NOT NULL DEFAULT '0',
  `response_error` int(11) NOT NULL DEFAULT '0',
  `remark` varchar(500) DEFAULT NULL,
  `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `update_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`credit_uuid`),
  UNIQUE KEY `bet_uuid_UNIQUE` (`bet_uuid`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

-- Data exporting was unselected.

-- Dumping structure for table p2p_common_m.tbl_trans_debit
CREATE TABLE IF NOT EXISTS `tbl_trans_debit` (
  `debit_uuid` varchar(64) NOT NULL,
  `bet_uuid` varchar(64) NOT NULL,
  `game_code` varchar(45) NOT NULL,
  `round_id` varchar(45) NOT NULL,
  `bet_pool` int(11) NOT NULL,
  `provider_code` varchar(64) NOT NULL,
  `merchant_code` varchar(64) NOT NULL,
  `currency_code` varchar(5) NOT NULL DEFAULT 'CNY',
  `player_id` varchar(64) NOT NULL,
  `client_id` varchar(45) NOT NULL DEFAULT '',
  `session_id` varchar(64) NOT NULL DEFAULT '',
  `debit_amount` decimal(19,4) NOT NULL DEFAULT '0.0000',
  `request_times` int(11) NOT NULL DEFAULT '0',
  `process_state` int(11) NOT NULL DEFAULT '0',
  `is_success` int(11) NOT NULL DEFAULT '0',
  `is_cancelled` int(11) NOT NULL DEFAULT '0',
  `network_error` int(11) NOT NULL DEFAULT '0',
  `response_error` int(11) NOT NULL DEFAULT '0',
  `remark` varchar(500) DEFAULT NULL,
  `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `update_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`debit_uuid`),
  UNIQUE KEY `bet_uuid_UNIQUE` (`bet_uuid`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

-- Data exporting was unselected.


-- Dumping database structure for p2p_game_main
CREATE DATABASE IF NOT EXISTS `p2p_game_main` /*!40100 DEFAULT CHARACTER SET utf8 */ /*!80016 DEFAULT ENCRYPTION='N' */;
USE `p2p_game_main`;

-- Dumping structure for table p2p_game_main.tbl_bo_account
CREATE TABLE IF NOT EXISTS `tbl_bo_account` (
  `merchant_code` varchar(50) NOT NULL,
  `account_id` varchar(64) NOT NULL,
  `account_pwd` varchar(64) NOT NULL,
  `is_active` int(11) NOT NULL DEFAULT '0',
  PRIMARY KEY (`merchant_code`,`account_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='backoffice accounts';

-- Data exporting was unselected.

-- Dumping structure for table p2p_game_main.tbl_game_record
CREATE TABLE IF NOT EXISTS `tbl_game_record` (
  `game_id` int(11) NOT NULL AUTO_INCREMENT,
  `server_code` varchar(45) NOT NULL,
  `table_code` varchar(45) NOT NULL,
  `shoe_code` varchar(45) NOT NULL,
  `round_number` int(11) NOT NULL DEFAULT '0',
  `round_state` int(11) NOT NULL DEFAULT '0',
  `is_cancelled` int(11) NOT NULL DEFAULT '0',
  `game_output` varchar(500) NOT NULL DEFAULT '',
  `game_result` varchar(500) NOT NULL DEFAULT '',
  `round_start_time` datetime DEFAULT NULL,
  `last_update_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`game_id`),
  UNIQUE KEY `UNI_GAME_ROUND` (`table_code`,`shoe_code`,`round_number`),
  KEY `IDX_SERVER_CODE` (`server_code`),
  KEY `IDX_ROUND_NUMBER` (`round_number`),
  KEY `IDX_ROUND_STATE` (`round_state`),
  KEY `IDX_GAME_RESULT` (`game_result`),
  KEY `IDX_START_TIME` (`round_start_time`),
  KEY `IDX_UPDATE_TIME` (`last_update_time`),
  KEY `IDX_SHOE_CODE` (`shoe_code`),
  KEY `IDX_TABLE_CODE` (`table_code`)
) ENGINE=InnoDB AUTO_INCREMENT=131 DEFAULT CHARSET=utf8;

-- Data exporting was unselected.

-- Dumping structure for table p2p_game_main.tbl_game_setting
CREATE TABLE IF NOT EXISTS `tbl_game_setting` (
  `setting_id` int(11) NOT NULL AUTO_INCREMENT,
  `server_code` varchar(45) NOT NULL,
  `table_code` varchar(45) NOT NULL,
  `table_setting` varchar(1000) DEFAULT NULL,
  `is_maintained` int(11) NOT NULL DEFAULT '0',
  PRIMARY KEY (`setting_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

-- Data exporting was unselected.

-- Dumping structure for table p2p_game_main.tbl_merchant_info
CREATE TABLE IF NOT EXISTS `tbl_merchant_info` (
  `merchant_code` varchar(45) NOT NULL,
  `currency_code` varchar(5) NOT NULL DEFAULT 'CNY',
  `api_url` varchar(500) DEFAULT NULL,
  `db_name` varchar(45) NOT NULL DEFAULT 'p2p_common_m',
  `api_service` varchar(45) NOT NULL DEFAULT 'merchant-api',
  `cpc_options` varchar(45) NOT NULL DEFAULT '0.05, 0.1, 0.2, 0.5, 1.0',
  `bpl_options` varchar(45) NOT NULL DEFAULT '1,2,3,4,5',
  `is_active` int(11) NOT NULL DEFAULT '0',
  `is_maintained` int(11) NOT NULL DEFAULT '0',
  PRIMARY KEY (`merchant_code`,`currency_code`),
  KEY `IDX_ACTIVE_STATE` (`is_active`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

-- Data exporting was unselected.


-- Dumping database structure for p2p_sample_m
CREATE DATABASE IF NOT EXISTS `p2p_sample_m` /*!40100 DEFAULT CHARACTER SET utf8 */ /*!80016 DEFAULT ENCRYPTION='N' */;
USE `p2p_sample_m`;

-- Dumping structure for table p2p_sample_m.tbl_bet_record
CREATE TABLE IF NOT EXISTS `tbl_bet_record` (
  `bet_uuid` varchar(64) NOT NULL,
  `debit_uuid` varchar(64) DEFAULT NULL,
  `credit_uuid` varchar(64) DEFAULT NULL,
  `provider_code` varchar(45) DEFAULT NULL,
  `game_code` varchar(45) DEFAULT NULL,
  `game_type` int(11) NOT NULL DEFAULT '0',
  `round_id` varchar(45) NOT NULL,
  `merchant_code` varchar(64) NOT NULL,
  `currency_code` varchar(5) NOT NULL DEFAULT 'CNY',
  `player_id` varchar(64) NOT NULL,
  `bet_type` int(11) NOT NULL DEFAULT '0',
  `bet_pool` int(11) NOT NULL DEFAULT '0',
  `bet_amount` decimal(19,4) NOT NULL DEFAULT '0.0000',
  `pay_amount` decimal(19,4) NOT NULL DEFAULT '0.0000',
  `game_input` varchar(500) NOT NULL DEFAULT '',
  `game_result` varchar(1000) NOT NULL DEFAULT '0',
  `settle_state` int(11) NOT NULL DEFAULT '0',
  `is_cancelled` int(11) NOT NULL DEFAULT '0',
  `bet_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `settle_time` datetime DEFAULT NULL,
  `cancel_time` datetime DEFAULT NULL,
  `update_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `remark` varchar(1000) DEFAULT NULL,
  PRIMARY KEY (`bet_uuid`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

-- Data exporting was unselected.

-- Dumping structure for table p2p_sample_m.tbl_player_balance
CREATE TABLE IF NOT EXISTS `tbl_player_balance` (
  `merchant_code` varchar(64) NOT NULL,
  `currency_code` varchar(5) NOT NULL DEFAULT 'CNY',
  `player_id` varchar(64) NOT NULL,
  `player_balance` decimal(19,4) NOT NULL DEFAULT '0.0000',
  `update_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`merchant_code`,`player_id`,`currency_code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

-- Data exporting was unselected.

-- Dumping structure for table p2p_sample_m.tbl_trans_cancel
CREATE TABLE IF NOT EXISTS `tbl_trans_cancel` (
  `trans_uuid` varchar(64) NOT NULL,
  `cancel_type` int(11) NOT NULL DEFAULT '0' COMMENT '0 - cancel debit, 1 - cancel credit',
  `target_uuid` varchar(45) NOT NULL,
  `amount` decimal(19,4) NOT NULL DEFAULT '0.0000',
  `update_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`trans_uuid`),
  KEY `IDX_UUID` (`target_uuid`),
  KEY `IDX_TYPE` (`cancel_type`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

-- Data exporting was unselected.

-- Dumping structure for table p2p_sample_m.tbl_trans_credit
CREATE TABLE IF NOT EXISTS `tbl_trans_credit` (
  `credit_uuid` varchar(64) NOT NULL,
  `bet_uuid` varchar(64) NOT NULL,
  `provider_code` varchar(45) DEFAULT NULL,
  `game_code` varchar(45) DEFAULT NULL,
  `round_id` varchar(45) NOT NULL,
  `merchant_code` varchar(64) NOT NULL,
  `currency_code` varchar(5) NOT NULL DEFAULT 'CNY',
  `player_id` varchar(64) NOT NULL,
  `bet_pool` int(11) NOT NULL,
  `credit_amount` decimal(19,4) NOT NULL DEFAULT '0.0000',
  `credit_success` int(11) NOT NULL DEFAULT '0',
  `credit_state` int(11) NOT NULL DEFAULT '0',
  `is_cancelled` int(11) NOT NULL DEFAULT '0',
  `cancel_success` int(11) NOT NULL DEFAULT '0',
  `last_return_code` int(11) NOT NULL DEFAULT '0',
  `bet_settle_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `cancel_time` datetime DEFAULT NULL,
  `update_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `remark` varchar(500) DEFAULT NULL,
  PRIMARY KEY (`credit_uuid`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

-- Data exporting was unselected.

-- Dumping structure for table p2p_sample_m.tbl_trans_debit
CREATE TABLE IF NOT EXISTS `tbl_trans_debit` (
  `debit_uuid` varchar(64) NOT NULL,
  `bet_uuid` varchar(64) NOT NULL,
  `provider_code` varchar(45) DEFAULT NULL,
  `game_code` varchar(45) DEFAULT NULL,
  `round_id` varchar(45) NOT NULL,
  `merchant_code` varchar(64) NOT NULL,
  `currency_code` varchar(5) NOT NULL DEFAULT 'CNY',
  `player_id` varchar(64) NOT NULL,
  `bet_pool` int(11) NOT NULL,
  `debit_amount` decimal(19,4) NOT NULL DEFAULT '0.0000',
  `debit_success` int(11) NOT NULL DEFAULT '0',
  `debit_state` int(11) NOT NULL DEFAULT '0',
  `is_cancelled` int(11) NOT NULL DEFAULT '0',
  `cancel_success` int(11) NOT NULL DEFAULT '0',
  `last_return_code` int(11) NOT NULL DEFAULT '0',
  `bet_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `cancel_time` datetime DEFAULT NULL,
  `update_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `remark` varchar(500) DEFAULT NULL,
  PRIMARY KEY (`debit_uuid`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

-- Data exporting was unselected.


-- Dumping database structure for p2p_sharp_node
CREATE DATABASE IF NOT EXISTS `p2p_sharp_node` /*!40100 DEFAULT CHARACTER SET utf8 */ /*!80016 DEFAULT ENCRYPTION='N' */;
USE `p2p_sharp_node`;

-- Dumping structure for table p2p_sharp_node.tbl_server_info
CREATE TABLE IF NOT EXISTS `tbl_server_info` (
  `server_name` varchar(32) NOT NULL,
  `group_name` varchar(32) NOT NULL,
  `server_url` varchar(64) NOT NULL COMMENT 'internal access entry',
  `public_url` varchar(128) DEFAULT NULL COMMENT 'public access entry',
  `public_protocol` int(11) NOT NULL DEFAULT '0' COMMENT '0: none, 1: http, 2: ws, 3: https, 4: wss',
  `client_count` int(11) NOT NULL DEFAULT '0' COMMENT 'total number of ws or wss connections',
  `visibility` int(11) NOT NULL DEFAULT '1',
  `public_visibility` int(11) NOT NULL DEFAULT '1',
  `service_list` varchar(1024) NOT NULL,
  `access_key` varchar(64) NOT NULL,
  `update_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`server_name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='NDB_TABLE=READ_BACKUP=1';

-- Data exporting was unselected.

/*!40101 SET SQL_MODE=IFNULL(@OLD_SQL_MODE, '') */;
/*!40014 SET FOREIGN_KEY_CHECKS=IF(@OLD_FOREIGN_KEY_CHECKS IS NULL, 1, @OLD_FOREIGN_KEY_CHECKS) */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
